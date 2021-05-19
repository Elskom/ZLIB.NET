# Security Policy

## Supported Versions

Any and all security updates are applied to the main branch, if they do not break the api,
then the last number in the version would be increased. However if they do have a breaking change,
the second number in the version will increase. Because of this, packages currently on nuget with
the vulnerability will sadly stay like that unless the users update the package to resolve it.

| Version | Supported          |
| ------- | ------------------ |
| main branch | :white_check_mark: |
| 1.1.4 | :x:                |
| < 1.1.4 | :x:                |

## Reporting a Vulnerability

File an issue on the github stating the vulnerability, and optionally you can open a pull request.

Usually if the CI's pass it gets merged, However I like to try to have close to 0 warnings if possible.

But if you do not want to or cannot open a pull request that is fine too, just note that it might take a few days
or so to fix it sometimes because debugging, or w/e could slow things down.
