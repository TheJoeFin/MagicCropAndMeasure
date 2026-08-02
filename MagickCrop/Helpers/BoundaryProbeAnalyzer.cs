using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Finds where a boundary crosses a probe line — the user drags a short segment
/// perpendicular to an edge, and this locates the transition along it, so a construction
/// point lands on the edge of the paper rather than near it.
///
/// Pure static math over <see cref="Point"/> and an image buffer; no WPF elements, no
/// control dependencies, in the same spirit as <see cref="ConstructionSolver"/>.
/// </summary>
/// <remarks>
/// This is a one-dimensional derivative-of-Gaussian edge detector: smooth, differentiate,
/// take the peak. That is the same maths Canny performs internally, without the non-maximum
/// suppression and hysteresis thresholding that reduce it to a binary mask — those throw
/// away exactly the sub-pixel position this needs, and their thresholds are tuned against
/// the whole image, so a faint paper edge often does not survive them.
///
/// It runs over colour rather than brightness, because a brightness step is ambiguous
/// evidence: a shadow falling across one sheet of paper produces a strong one with no
/// boundary behind it. A change of hue rarely happens without a change of material, so
/// chroma is the more trustworthy signal and is weighted accordingly.
/// </remarks>
public static class BoundaryProbeAnalyzer
{
    /// <summary>A probe shorter than this cannot be told apart from a click.</summary>
    private const double MinProbeLength = 4.0;

    /// <summary>Samples per pixel of probe length, so the answer can land between pixels.</summary>
    private const double SamplesPerPixel = 2.0;

    private const int MinSamples = 16;
    private const int MaxSamples = 512;

    /// <summary>
    /// Half-width of the band of parallel scan lines, in pixels. Averaging across lines
    /// laid along the boundary is the single biggest noise win available here: a real
    /// straight edge reinforces across every lane while grain and texture cancel.
    /// </summary>
    private const double LaneHalfWidth = 4.0;

    /// <summary>Lanes across the band. Odd so one runs down the probe itself.</summary>
    private const int LaneCount = 9;

    /// <summary>
    /// Standard deviation of the smoothing kernel, in samples. Small — the buffer is
    /// already lightly blurred, and over-smoothing drags a boundary toward whatever else
    /// is nearby.
    /// </summary>
    private const double SmoothingSigma = 1.2;

    private const int SmoothingRadius = 3;

    /// <summary>
    /// How much more a change of hue counts than an equal-sized change of brightness.
    /// Above one on purpose: brightness varies across a single surface through shading and
    /// shadow, so a luminance step is weak evidence of a boundary, while two materials
    /// meeting almost always changes hue.
    /// </summary>
    private const double ChromaWeight = 1.5;

    /// <summary>
    /// Weight given to a candidate at the very ends of the probe, relative to one at the
    /// middle. The user aims the middle of the drag at the boundary, so the middle is much
    /// more likely to be right — but this stays well above zero so a genuinely stronger
    /// edge near an end can still win.
    /// </summary>
    private const double CenterWeightFloor = 0.35;

    /// <summary>
    /// Fraction of the peak gradient that still counts as part of the same transition.
    /// The run above this is what gets averaged to find the middle of a gradient.
    /// </summary>
    private const double HalfMaximum = 0.5;

    /// <summary>How far the peak must stand out from the profile's own texture to score full marks.</summary>
    private const double PeakRatioTarget = 4.0;

    /// <summary>Colour range across the probe, 0-1, that scores full marks for contrast.</summary>
    private const double ContrastTarget = 0.08;

    /// <summary>Below this the result is offered but flagged, rather than trusted.</summary>
    private const double WeakConfidence = 0.35;

    /// <summary>A probe flatter than this (in 0-255 units) has nothing to find.</summary>
    private const double FlatProfileRange = 0.5;

    /// <summary>Keeps the result off the exact endpoints, where it would look like a failure.</summary>
    private const double MinT = 0.02;
    private const double MaxT = 0.98;

    /// <param name="Position">The boundary position, in the same space as the probe endpoints.</param>
    /// <param name="T">Where it fell along the probe, 0 at the start and 1 at the end.</param>
    /// <param name="Confidence">0-1. Combines how sharply the peak stands out with how much colour change there was to work with.</param>
    /// <param name="IsWeak">True when the result is a best guess rather than a clear boundary.</param>
    public readonly record struct BoundaryProbeResult(Point Position, double T, double Confidence, bool IsWeak);

    /// <summary>
    /// Locates the boundary crossing the probe from <paramref name="startPixel"/> to
    /// <paramref name="endPixel"/>, both in image pixel coordinates.
    /// </summary>
    /// <returns>
    /// False only when the probe is unusable — too short, or off a null buffer. A probe
    /// across a blank wall still returns true, at the midpoint, flagged weak: the gesture
    /// was well formed and the user gets something to nudge.
    /// </returns>
    public static bool TryFindBoundary(
        ImageSampleBuffer? image,
        Point startPixel,
        Point endPixel,
        out BoundaryProbeResult result)
    {
        result = default;

        if (image is null) return false;

        double dx = endPixel.X - startPixel.X;
        double dy = endPixel.Y - startPixel.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (double.IsNaN(length) || double.IsInfinity(length) || length < MinProbeLength)
            return false;

        Profiles profiles = SampleProfiles(image, startPixel, dx, dy, length);

        // Nothing but noise along the whole probe: hand back the middle rather than an
        // arbitrary argmax over a flat array.
        if (profiles.ContrastRange < FlatProfileRange)
        {
            result = BuildResult(startPixel, dx, dy, 0.5, 0.0);
            return true;
        }

        double[] gradient = CombinedGradient(profiles);
        int peakIndex = FindWeightedPeak(gradient);

        if (peakIndex < 0)
        {
            result = BuildResult(startPixel, dx, dy, 0.5, 0.0);
            return true;
        }

        double centerIndex = FindGradientCenter(gradient, peakIndex);
        double t = Math.Clamp(centerIndex / (gradient.Length - 1), MinT, MaxT);
        double confidence = ScoreConfidence(gradient, peakIndex, profiles.ContrastRange);

        result = BuildResult(startPixel, dx, dy, t, confidence);
        return true;
    }

    /// <summary>
    /// The probe reduced to three signals, in an opponent-colour space: how light it is,
    /// how red against green, and how blue against yellow. Splitting colour this way is
    /// what lets brightness and hue be weighed against each other instead of being mixed
    /// together and lost.
    /// </summary>
    /// <param name="ContrastRange">
    /// How much colour varies along the whole probe, already weighted, in 0-255 units.
    /// Drives the confidence score and the flat-probe test.
    /// </param>
    private readonly record struct Profiles(
        double[] Luma,
        double[] RedGreen,
        double[] BlueYellow,
        double ContrastRange);

    /// <summary>
    /// Splits a colour into brightness and two hue axes, each spanning the same 0-255
    /// range so <see cref="ChromaWeight"/> is the only thing tipping the balance between
    /// them, rather than an accident of the encoding.
    /// </summary>
    private static void ToOpponent(
        double red, double green, double blue,
        out double luma, out double redGreen, out double blueYellow)
    {
        luma = (0.299 * red) + (0.587 * green) + (0.114 * blue);
        redGreen = (red - green) / 2.0;
        blueYellow = (blue - ((red + green) / 2.0)) / 2.0;
    }

    /// <summary>
    /// Averages colour along a band of lines parallel to the probe. The band is capped at
    /// half the probe length so a short probe placed near a corner does not smear the
    /// corner into its own reading.
    /// </summary>
    private static Profiles SampleProfiles(
        ImageSampleBuffer image,
        Point start,
        double dx,
        double dy,
        double length)
    {
        int n = Math.Clamp((int)Math.Round(length * SamplesPerPixel), MinSamples, MaxSamples);

        // Perpendicular to the probe is parallel to the boundary, which is the direction
        // the lanes spread along.
        double perpX = -dy / length;
        double perpY = dx / length;

        double halfWidth = Math.Min(LaneHalfWidth, length / 4.0);
        int laneCount = halfWidth >= 1.0 ? LaneCount : 1;
        double laneSpacing = laneCount > 1 ? (2 * halfWidth) / (laneCount - 1) : 0;

        double[] luma = new double[n];
        double[] redGreen = new double[n];
        double[] blueYellow = new double[n];

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / (n - 1);
            double x = start.X + (dx * t);
            double y = start.Y + (dy * t);

            double sumRed = 0, sumGreen = 0, sumBlue = 0;

            for (int lane = 0; lane < laneCount; lane++)
            {
                double offset = laneCount > 1 ? -halfWidth + (lane * laneSpacing) : 0;

                image.SampleBilinear(
                    x + (perpX * offset), y + (perpY * offset),
                    out double red, out double green, out double blue);

                sumRed += red;
                sumGreen += green;
                sumBlue += blue;
            }

            ToOpponent(
                sumRed / laneCount, sumGreen / laneCount, sumBlue / laneCount,
                out luma[i], out redGreen[i], out blueYellow[i]);
        }

        Smooth(luma);
        Smooth(redGreen);
        Smooth(blueYellow);

        // How much the probe's colour varies end to end, on the same weighted footing the
        // gradient uses. This stands in for the local auto-level a single-channel version
        // would do: what matters is contrast across this probe, not across the photo, so a
        // white sheet on a pale desk still reads as having something to find.
        double lumaRange = Range(luma);
        double redGreenRange = Range(redGreen) * ChromaWeight;
        double blueYellowRange = Range(blueYellow) * ChromaWeight;

        double contrastRange = Math.Sqrt(
            (lumaRange * lumaRange) +
            (redGreenRange * redGreenRange) +
            (blueYellowRange * blueYellowRange));

        return new Profiles(luma, redGreen, blueYellow, contrastRange);
    }

    private static double Range(double[] profile)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (double value in profile)
        {
            if (value < min) min = value;
            if (value > max) max = value;
        }

        return max - min;
    }

    /// <summary>
    /// How fast the colour is changing at each point along the probe: the length of the
    /// per-channel derivative vector, with the two hue axes scaled up by
    /// <see cref="ChromaWeight"/>.
    /// </summary>
    /// <remarks>
    /// Combining the channels as a vector length rather than summing them means a
    /// transition shows up whichever channel carries it, so yellow meeting blue reads as
    /// strongly as black meeting white — and a hue change with no brightness change, which
    /// a greyscale reading cannot see at all, reads more strongly still.
    /// </remarks>
    private static double[] CombinedGradient(Profiles profiles)
    {
        int n = profiles.Luma.Length;
        double[] gradient = new double[n];

        for (int i = 1; i < n - 1; i++)
        {
            double luma = Derivative(profiles.Luma, i);
            double redGreen = Derivative(profiles.RedGreen, i) * ChromaWeight;
            double blueYellow = Derivative(profiles.BlueYellow, i) * ChromaWeight;

            gradient[i] = Math.Sqrt(
                (luma * luma) + (redGreen * redGreen) + (blueYellow * blueYellow));
        }

        return gradient;
    }

    /// <summary>
    /// Central difference. Signed here — the channels are combined as a vector length, so
    /// the sign of each one matters until they are put together.
    /// </summary>
    private static double Derivative(double[] profile, int index) =>
        (profile[index + 1] - profile[index - 1]) / 2.0;

    /// <summary>Gaussian smoothing in place, clamping at the ends.</summary>
    private static void Smooth(double[] profile)
    {
        double[] kernel = new double[(SmoothingRadius * 2) + 1];
        double kernelSum = 0;

        for (int k = -SmoothingRadius; k <= SmoothingRadius; k++)
        {
            double weight = Math.Exp(-(k * k) / (2 * SmoothingSigma * SmoothingSigma));
            kernel[k + SmoothingRadius] = weight;
            kernelSum += weight;
        }

        double[] source = (double[])profile.Clone();

        for (int i = 0; i < profile.Length; i++)
        {
            double sum = 0;
            for (int k = -SmoothingRadius; k <= SmoothingRadius; k++)
            {
                int index = Math.Clamp(i + k, 0, source.Length - 1);
                sum += source[index] * kernel[k + SmoothingRadius];
            }

            profile[i] = sum / kernelSum;
        }
    }

    /// <summary>
    /// Strongest gradient after weighting toward the middle of the probe. Returns -1 when
    /// the probe is entirely flat.
    /// </summary>
    private static int FindWeightedPeak(double[] gradient)
    {
        int n = gradient.Length;
        int best = -1;
        double bestScore = 0;

        for (int i = 1; i < n - 1; i++)
        {
            if (gradient[i] <= 0) continue;

            double t = (double)i / (n - 1);

            // Raised cosine: 1 at the middle, CenterWeightFloor at either end.
            double centered = Math.Cos(Math.PI / 2 * ((2 * t) - 1));
            double weight = CenterWeightFloor + ((1 - CenterWeightFloor) * centered * centered);

            double score = gradient[i] * weight;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// The middle of the transition rather than its steepest point: walk out from the peak
    /// while the gradient holds above half its maximum, then take the gradient-weighted
    /// centroid of that run.
    /// </summary>
    /// <remarks>
    /// On a sharp step edge the run is one or two samples wide and this agrees with the
    /// peak. On a soft or slightly asymmetric gradient — a shadow, a defocused edge, the
    /// rolled edge of a stack of paper — it lands mid-ramp, which is where the boundary
    /// actually is and where a peak-only answer would drift.
    /// </remarks>
    private static double FindGradientCenter(double[] gradient, int peakIndex)
    {
        double threshold = gradient[peakIndex] * HalfMaximum;

        int low = peakIndex;
        while (low - 1 >= 1 && gradient[low - 1] >= threshold)
            low--;

        int high = peakIndex;
        while (high + 1 <= gradient.Length - 2 && gradient[high + 1] >= threshold)
            high++;

        double weightedSum = 0;
        double weightSum = 0;

        for (int i = low; i <= high; i++)
        {
            weightedSum += i * gradient[i];
            weightSum += gradient[i];
        }

        return weightSum > 0 ? weightedSum / weightSum : peakIndex;
    }

    /// <summary>
    /// Two independent ways the reading can be untrustworthy, multiplied: the peak may not
    /// stand out from the profile's own texture, and there may not have been much colour
    /// change to read in the first place. Either one alone is enough to make the answer a
    /// guess.
    /// </summary>
    private static double ScoreConfidence(double[] gradient, int peakIndex, double contrastRange)
    {
        int n = gradient.Length;
        double sum = 0;
        int count = 0;

        for (int i = 1; i < n - 1; i++)
        {
            sum += gradient[i];
            count++;
        }

        double mean = count > 0 ? sum / count : 0;
        double peakRatio = mean > 0 ? gradient[peakIndex] / mean : 0;

        double sharpness = Math.Min(peakRatio / PeakRatioTarget, 1.0);
        double contrast = Math.Min(contrastRange / 255.0 / ContrastTarget, 1.0);

        return Math.Clamp(sharpness * contrast, 0, 1);
    }

    private static BoundaryProbeResult BuildResult(Point start, double dx, double dy, double t, double confidence)
    {
        Point position = new(start.X + (dx * t), start.Y + (dy * t));

        return new BoundaryProbeResult(position, t, confidence, confidence < WeakConfidence);
    }
}
