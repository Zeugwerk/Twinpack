# Twinpack - The Library Package Manager for TwinCAT

The Twinpack Package Manager is a powerful and user-friendly package management tool. Whether you are a seasoned TwinCAT developer or just getting started, Twinpack empowers you to extend the capabilities of your TwinCAT projects by providing a seamless library management experience. Enhance your productivity, foster collaboration, and unlock the full potential of TwinCAT with this indispensable tool.

## Table of Contents

- [Concept](#concept)
- [Installation](#installation)
- [Using a Package](#using-a-package)
- [Sharing a Package](#sharing-a-package)
- [Disclaimer](#disclaimer)

## Concept

Twinpack is designed to facilitate the sharing of TwinCAT libraries within the community. It provides a centralized repository of libraries contributed by the community, allowing you to enhance your projects with pre-built functionality and accelerate development and to share your libraries with thecommunity. Keep track of library versions and easily switch between different versions within your projects. Twinpack ensures that you have full control over the libraries you use, allowing you to maintain compatibility and stability as your projects evolve.

## Installation

To use the Twinpack Package Manager, follow these steps:

1. Download the latest installer.
2. In the installation process you will be asked to optionally register yourself for publishing your own packages.
3. Twinpack supports multiple versions of Visual Studio and TwinCAT XAE Shell. Select the IDEs, which Twinpack should be installed for.
4. Click on the "Install".
5. Follow the on-screen instructions to complete the installation.

## Using a Package

To install a package from the Twinpack Server, follow these steps:

1. Open a TwinCAT solution and navigate to a PLC.
2. Right click the References item of your PLC
3. Click 'Twinpack Catalog...'
4. Browse or search for the desired package.
5. Click on the package to view details.
6. Click on the "Install" button to install the package.
7. Wait for the installation process to complete. If you are installing packages for the first time and they come with a license you will be asked to confirm their license agreement in order to advance.
8. Twinpack automatically installed the package on your System and added it as a reference to your PLC.
9. Follow the library documentation or instructions to incorporate its functionality into your project.

## Sharing a Package

To share your own TwinCAT library as a package with the TwinCAT community, please follow these guidelines:

1. Open a TwinCAT solution and navigate to the PLC library you want to share with the community
2. Right click the PLC item and click 'Twinpack' -> 'Publish ...'
3. In the dialog, which opens, fill in the information describing your package.
   - Distributor (mandatory): The name you enter here will be exclusively associated with your Twinpack Account, and it will serve as a unique identifier for all your upcoming packages.
   - Version (mandatory): This is the initial version of your package. Once you publish your package for the first time, you have the flexibility to release newer versions in the future.
   - The Advanced menu is specifically designed for enterprise users of Twinpack. It enables them to configure packages for private usage by controlling the audience that can access and download the package (i.e. emloyees of a company). [Contact us](mailto:info@zeugwerk.at) if you are interested in this feature.
   - Other information is optional and self-explanatory. However, note that all information, except for 'notes', is linked to a package rather than a specific version of a package. The latter may be used to give a short changelog so users know what changed since the previous release.
5. Click publish to make Twinpack
   - Check all objects of your library
   - Upload the library as a package to the Twinpack server making it available for the communtity
  
After the inital upload of your library as a package you may publish newer versions of your library. Users of Twinpack will be notified automatically whenever a newer version is available when they open the Twinpack Catalog.

Please note that your package should meet certain standards and guidelines to ensure its quality and compatibility with TwinCAT.

