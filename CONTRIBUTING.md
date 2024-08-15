# Twinpack Contribution Guidelines

Thank you for your interest in contributing to Twinpack! We welcome contributions of all kinds—whether they are bug reports, 
new features, documentation improvements, unit tests or system tests. To maintain a high quality of contributions and streamline 
the development process, we as maintainers follow a specific workflow. Please read through the following guidelines before getting started. Note that we are not strict regarding this flow, every contribution is welcome, wheather this guideline is followed or not by the contributor.
The main purpose of this page is so that you understand how we, as the maintainers of this project, develop this project.

---

## Table of Contents
1. [Getting Started](#getting-started)
1. [Branching Strategy](#branching-strategy)
1. [Conventional Commits](#conventional-commits)
1. [Pull Request Process](#pull-request-process)
1. [Testing](#testing)
1. [Issue Reporting](#issue-reporting)

---

## Getting Started

1. **Fork the repository** to your own GitHub account.
2. **Clone your fork**:
   ```bash
   git clone https://github.com/Zeugwerk/Twinpack.git
   cd Twinpack
   ```
3. Install dependencies according to the project's README.
4. Create a new branch to work on a specific feature or bugfix (see Branching Strategy).


## Branching Strategy
We use a branching strategy, which is a combination of Release Flow and trunk-based development . Here's how to manage your branches:

* **main branch:** This branch contains the latest development code and features that are under active development. Usually features and fixes are developed directly in the main branch (if they have only few number of expected commits). **This branch might not always be in a stable state**. 
* **release branch:** Branches prefixed with release/ are used to stabilize a specific release. Releases are made directly from these branches, and they are never merged back into main. After a release, fixes in main (or fix branches) should be cherry-picked or backported manually and vice-versa.
* **feature branches:** Use feature branches for new features or enhancement. Branch off from main using the prefix feature/. Example: feature/my-new-feature. Usually releases are closed regarding features and so, **will not** be cherry-picked to a release branch.
* **fix branches:** Use fix branches for fixes. Branch off from main using the prefix fix/. Example: fix/fix-crash-on-startup. If possible, fixes will be cherry-picked to at least to the latest, affected release branch.


## Conventional Commits
We use the Conventional Commits specification to enforce a consistent commit history. For every commit that is merged into main or a release, the commit message must have the following format.
Note that this does not include commits in branch IF the commits are squashed before merging.

```
<type>(<scope>): <description>

[optional body]
[optional tags like #nodoc]

[optional footer(s)]
```

The `<type>` and `<description>` fields are mandatory, the `(<scope>)` field is optional.

* Type
The type is the most important keyword in the commit message. It is at the first position of the message. It has to be one of the following:

  * **ci**: Changes to our CI configuration files and scripts
  * **doc**: Documentation only changes
  * **feat**: A new feature
  * **fix**: A bug fix
  * **refactor**: A code change that neither fixes a bug nor adds a feature
  * **test**: Adding missing tests or correcting existing tests

If the commit is changing the API or ABI an exclamation point should be appended to the type (i.e. **feat!**). Note that API changes are usually only allowed in the development branch (main).

* **Scope:** The scope is optional, but if used, should be the area of the codebase your change affects (e.g., api, auth, build).
* **Description and body:** A concise description of the change (lowercase and without a period).
* **Footer:** Used for cherry-picks (use git cherry-pick -x)

## Pull Request Process

1. Create a pull request from your feature/fix branch to the main or a release branch.
2. Ensure your pull request:

  * Includes appropriate tests for any new features or bug fixes.
  * Has proper documentation updates if the changes affect existing functionality or introduce new features.
    
3. All pull requests require at least one code review approval from a maintainer before they can be merged.
4. Releases will be handled from release branches. After a release is made, any necessary changes to the main branch will be cherry-picked or backported by maintainers and vice-versa.
5. When your pull request is approved and ready to merge, a maintainer will handle the merge, usually by squashing commits for a cleaner history. The squash commit will adhere to the Conventional Commits guidelines


## Testing
* Make sure to write tests for any new features or bug fixes.
* Run all tests locally before submitting your pull request to ensure nothing is broken.
* If adding a new feature, consider writing unit tests or system tests to cover the feature.


## Issue reporting

Before submitting a new issue, please:

1. Search the issue tracker to check if the issue has already been reported.
2. If it's a new issue, include the following:
   
  * A detailed description of the problem.
  * Steps to reproduce the issue.
  * Expected behavior.
  * Any relevant logs, screenshots, or error messages.
  * Your environment details (e.g., Version of the software, TwinCAT version you use, etc.).


---
Thank you for contributing to Twinpack! By following these guidelines, you help ensure a smooth and efficient development process. If you have any questions or need further clarification, feel free to reach out to the maintainers. Happy coding!
