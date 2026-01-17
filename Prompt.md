## Your Task

1. **Find work**: Study `migration-specs\*` using parallel subagents. Pick the most important incomplete task based on dependencies in `migration-specs\README.md`. Study the codebase structure. Also review `ISSUES.md` for open issues, when resolved append to the end of the issue file stating which issue has been resolved.
2. **Do work**: Implement the single highest priority feature using subagents. Use up to 20 subagents in parallel to read files and write files. Use only 1 subagent to run tests. Make sure every new line of code is correct and tested.
3. **Update as you go**:

   * Mark completed items in the spec you're working on
   * Keep `migration-specs\README.md` accurate
   * Update `CLAUDE.md` when you learn something new (commands, app-specific details) - keep it brief
   * Archive completed specs to `migration-spec\archive\`
   * If you get blocked, document the blocker in the spec and exit

4. Before making changes, search the codebase using parallel subagents. Don't assume something isn't implemented.
5. When you learn something new about the app, update CLAUDE.md using a subagent but keep it brief.
6. **Commit after completing work**. Write clear commit messages. Use `git add -A \\\&\\\& git commit -m "message"`. Make sure all tests pass first!
7. **Exit after one task**. After completing and committing ONE feature, exit immediately. Do not pick up another task. The outer loop will start a fresh agent for the next iteration.

## CRITICAL

DO NOT IMPLEMENT PLACEHOLDER OR SIMPLE IMPLEMENTATIONS. WE WANT FULL IMPLEMENTATIONS.

