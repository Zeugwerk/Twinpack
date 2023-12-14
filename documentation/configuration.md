# Configuration file (.Zeugwerk/config.json)

Twinpack is designed to work out-of-the-box. By default Twinpack will parse your plcproj file everytime do open the [Twinpack Catalog](#using-a-package) or [publish a package](#sharing-a-package), for every reference that you have in our plcproj it will check if there is an appropriate package available on Twinpack. If no, it will simply ignore this reference, if yes, it will list it in the Catalog in the "Installed" tab.

Parsing and resolving packages everytime is a bit slow and so, to enhance the performance of Twinpack it is possible to let Twinpack create a configuration file for you, which will be placed in the folder `.Zeugwerk\config.json`. The file itself contains meta project for your solution (name of the PLC, Version of the PLC, used Twinpack Packages).

To generate the config file, navigate to the [Twinpack Catalog](#using-a-package) and click the `Create` button. The configuration file will give you the following benefits

- Opening the Twinpack Catalog is much faster
- Integration with [zkbuild](https://github.com/Zeugwerk/zkbuild-action) for automatically building your project and optionally creating a TwinCAT library whenever you commit to GitHub
- Your TwinCAT Solution can directly be used with [zkdoc](https://github.com/Zeugwerk/zkdoc-action) to create a documentation for your PLC right on GitHub (or any other target), for instance see [here](https://stefanbesler.github.io/struckig/).

A typcial configuration file for a solution with 1 PLC looks like this (Twinpack generates this for you automatically)

```json
{
  "fileversion": 1,
  "solution": "TwinCAT Project1.sln",
  "projects": [
    {
      "name": "TwinCAT Project1",
      "plcs": [
        {
          "version": "1.0.0.0",
          "name": "Untitled1",
          "type": "Application",
          "packages": [
            {
              "version": "1.2.19.0",
              "repository": "bot",
              "name": "ZCore",
              "branch": "release/1.2",
              "target": "TC3.1",
              "configuration": "Distribution",
              "distributor-name": "Zeugwerk GmbH"
            }
          ],
          "references": {
            "*": [
              "Tc2_Standard=*",
              "Tc2_System=*",
              "Tc3_Module=*"
            ]
          }
        }
      ]
    }
  ]
}
```


## Schema

```json
{
  "$schema": "http://json-schema.org/twinpack-config/schema#",
  "type": "object",
  "description": "The schema for a Twinpack configuration file data.",
  "properties": {
    "fileversion": {
      "type": "integer",
      "description": "The version of the file"
    },
    "solution": {
      "type": "string",
      "description": "Filename of the Visual Studio / XaeShell solution including the file extension, e.g. Untitled1.sln"
    },
    "projects": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "description": "The name of the project in the solution"
          },
          "plcs": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "version": {
                  "type": "string",
                  "description": "Version of the PLC. This is equivalent to the version of the PLC project information in TwinCAT"
                },
                "distributor-name": {
                  "type": "string",
                  "description": "The name of the distributor. This is equivalent to the Company of the PLC project information in TwinCAT."
                },
                "name": {
                  "type": "string",
                  "description": "The name of the PLC. This is equivalent to the Title of the PLC project information in TwinCAT."
                },
                "type": {
                  "type": "string",
                  "description": "['Library', 'FrameworkLibrary', 'Application', 'Test']"
                },
                "packages": {
                  "type": "array",
                  "description": "List of dependencies"
                  "items": {
                    "type": "object",
                    "properties": {
                      "distributor-name": {
                        "type": "string",
                        "description": "The name of the distributor."
                      },
                      "name": {
                        "type": "string",
                        "description": "The name of the package."
                      },
                      "version": {
                        "type": "string",
                        "description": "The version of the package."
                      },
                      "repository": {
                        "type": "string",
                        "description": "(Deprecated) The repository of the package - this has been superseded by the distributor-name"
                      },
                      "branch": {
                        "type": "string",
                        "description": "(Optional) The branch of the package. This can be used for Feature branches, Bugfixing branches, it default to main"
                      },
                      "target": {
                        "type": "string",
                        "description": "(Optional) The target platform of the package. Defaults to 'TC3.1', which indicates the that package that is build for the latest version of Twinpack should be used"
                      },
                      "configuration": {
                        "type": "string",
                        "description": "(Optional) The configuration of the package. Default to 'Release', this can be used to distribute packages with different patches"
                      },

                      "qualified-only": {
                        "type": "boolean",
                        "description": "(Optional) Flag if the package should be inserted with the qualified-only attribute set, such that the namespace has to be explictly given"
                      },
                      "framework": {
                        "type": "string",
                        "description": "The framework to which the package belongs."
                      }
                    },
                    "required": ["version", "name", "distributor-name"],
                    "description": "Details about the packages used in the PLC."
                  }
                },
                "references": {
                  "type": "object",
                  "description": "Key=Value pairs for every possible target of the package"
                  "properties": {
                    "TC3.1": {
                      "type": "array",
                      "items": {
                        "type": "string"
                      },
                      "description": "References for TC3.1 platform, the references have to be in the form of Library=Version, e.g. Tc2_System=1.0.1.1"
                    }
                  },
                  "required": [],
                  "description": "References in the PLC for different platforms."
                }
              },
              "required": ["distributor-name", "name", "version", "type"],
              "description": "Details about the PLCs in the project."
            }
          }
        },
        "required": ["name", "plcs"],
        "description": "Details about the projects in the solution."
      }
    }
  },
  "required": ["fileversion", "solution", "projects"],
}

```
