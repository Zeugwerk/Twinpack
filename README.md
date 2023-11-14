# Twinpack - The Library Package Manager for TwinCAT

The Twinpack Package Manager is a powerful and user-friendly package management tool for TwinCAT libraries. It is designed to empower the TwinCAT community by enabling seamless sharing and distribution of libraries, fostering collaboration, and facilitating the exchange of specialized components among developers. It acts as a versatile platform akin to NuGet, allowing users to efficiently manage and deploy their custom-built modules.

With TwinCAT 3.1.4026, Beckhoff introduced a package manager which emphasizes the installation and maintenance of the TwinCAT Integrated Development Environment (IDE) and other software components tightly integrated with Beckhoff's proprietary offerings. While Twinpack and Beckhoff's package manager contribute to the advancement of TwinCAT technology, Twinpack stands out for its community-driven ethos, encouraging innovation, customization, and knowledge exchange among users, while Beckhoff's package manager centers on providing a streamlined experience for their official software installations.

## Table of Contents

- [Quicklinks](#quicklinks)
- [Installation](#installation)
- [Using a Package](#using-a-package)
- [Sharing a Package](#sharing-a-package)
- [Further information](#further-information)

## Quicklinks
- [Installer](https://github.com/Zeugwerk/Twinpack/releases/latest)
- [Registration](https://zeugwerk.dev/wp-login.php?action=register) (optional, only for publishing)
- [Password reset](https://zeugwerk.dev/wp-login.php?action=lostpassword) (optional)
- [Contact us](mailto:info@zeugwerk.at)

## Installation

To use the Twinpack Package Manager, follow these steps:

1. [Download](https://github.com/Zeugwerk/Twinpack/releases/latest) the latest installer.
2. In the installation process you will be asked to optionally register yourself for publishing your own packages. Type in a valid email address to get your login information into your mailbox right after the installation of Twinpack. This is needed for publishing libraries later on.
3. Twinpack supports multiple versions of Visual Studio and TwinCAT XAE Shell. Select the IDEs, which Twinpack should be installed for.
4. Click on "Install".
5. Follow the on-screen instructions to complete the installation.

## Using a Package

<img align="center" src="/images/twinpack_catalog.png" width="800" />

To install a package from the Twinpack Server, follow these steps:

1. Open a TwinCAT solution and navigate to a PLC.
2. Right click the References item of your PLC
3. Click 'Twinpack Catalog...'
4. Browse or search for the desired package.
5. Click on the package to view details.
6. Click on the "Add" button to install this package and add it to the referenced libraries.
7. Wait for the installation process to complete. If you are installing packages from a contributor for the first time and these packages come with a license you will be asked to confirm their license agreement in order to advance.
8. Twinpack automatically installs the package, including all depending libraries, on your System and adds it as a reference to your PLC.
9. Follow the library documentation or instructions to incorporate its functionality into your project.

## Sharing a Package

<img align="center" src="/images/twinpack_publish.png" width="800" />

To share your own TwinCAT library as a package with the TwinCAT community, please follow these guidelines:

1. Open a TwinCAT solution and navigate to the PLC library you want to share with the community. It is recommended to have a TwinCAT library created in a TwinCAT PLC (-only) project instead of a TwinCAT XAE project.
2. Right click the PLC item and click 'Twinpack' -> 'Publish ...'
3. In the dialog, which opens, fill in the information describing your package.
   - Distributor (mandatory): The name you enter here will be exclusively associated with your Twinpack Account, and it will serve as a unique identifier for all your upcoming packages.
   - Version (mandatory): This is the initial version of your package. Once you publish your package for the first time, you have the flexibility to release newer versions in the future.
   - The Advanced menu is specifically designed for enterprise users of Twinpack. It enables them to configure packages for private usage by controlling the audience that can access and download the package (i.e. employees of a company). [Contact us](mailto:info@zeugwerk.at) if you are interested in this feature.
   - Other information is optional and self-explanatory. However, note that all information, except for 'notes', is linked to a package rather than a specific version of a package. The latter may be used to give a short changelog so users know what changed since the previous release.
   - if you have a library icon, make sure to integrate this in the PLC-project as a standard file in order not to loose it. The recommendation for the library icon is square size, png file-type and pixel-size should be 256x256. However, the icon can also be of larger size, it will be scaled automatically. If no icon is chosen, Twinpack will generate a randomized icon figure.
5. Click publish to make Twinpack
   - Check all objects of your library
   - Upload the library as a package to the Twinpack server making it available for the community
  
After the initial upload of your library as a package, you may publish newer versions of your library or you can also modify the current version by right clicking on the PLC item and then on 'Twinpack' -> 'Modify...'. Users of Twinpack will be notified whenever a newer version is available in the Twinpack Catalog.

Please note that your package should meet certain standards and guidelines to ensure its quality and compatibility with TwinCAT. If you don't have any guidelines yet, [here](https://doc.zeugwerk.dev/contribute/contribute_code.html) are some suggestions.

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

## Further information

🌟 Make sure to follow this project by leaving a star or simply follow us, to always get notified if a newer version of Twinpack is released.

📺 We have also created a short introductionary video on how to install and use Twinpack (more to come):
- [Share TwinCAT libraries with Twinpack](https://youtu.be/xvJG9BRN610?si=RMMIPcdtMAoHkyGW)
