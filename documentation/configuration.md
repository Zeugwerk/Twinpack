## Configuration file (.Zeugwerk/config.json)

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
