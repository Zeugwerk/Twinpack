# Twinpack 

The Twinpack Package Manager is a powerful and user-friendly package management tool for TwinCAT libraries. It is designed to empower the TwinCAT community by enabling sharing and distribution of libraries. It acts as a versatile platform similarly to NuGet (but with a PLC touch), allowing users to efficiently manage and deploy their custom-built modules.

With TwinCAT 3.1.4026, Beckhoff introduced a package manager which emphasizes the installation and maintenance of the TwinCAT Integrated Development Environment (IDE) and other software components tightly integrated with Beckhoff's proprietary offerings. While Twinpack and Beckhoff's package manager contribute to the advancement of TwinCAT technology, Twinpack stands out for its community-driven ethos, encouraging innovation, customization, and knowledge exchange among users, while Beckhoff's package manager centers on providing a streamlined experience for their official software installations.

## Quicklinks
- [Installer](https://github.com/Zeugwerk/Twinpack/releases/latest)
- [Registration](https://zeugwerk.dev/wp-login.php?action=register) (optional, only needed for `twinpack push`)
- [Password reset](https://zeugwerk.dev/wp-login.php?action=lostpassword) (optional)
- [Contact us](mailto:info@zeugwerk.at)

## Installation

To use the Twinpack Package Manager, follow these steps:

1. [Download](https://github.com/Zeugwerk/Twinpack/releases/latest) the latest installer.
2. Select the IDEs, which Twinpack should be installed for.
3. Click on "Install".
4. Follow the on-screen instructions to complete the installation.

## Using a Package

<p float="left">
<img src="/images/twinpack_contextmenu.png" width="300" hspace="80" />
<img src="/images/twinpack_catalog.png" width="500" />
</p>


To install a package, follow these steps:

1. Open a TwinCAT solution and navigate to a PLC.
2. Right click the References item of your PLC
3. Click 'Twinpack Catalog...'
4. Browse or search for the desired package.
5. Click on the package to view details.
6. Click on the "Add" button to install this package and add it to the referenced libraries.
7. Wait for the installation process to complete. If you are installing packages from a contributor for the first time and these packages come with a license you will be asked to confirm their license agreement in order to advance.
8. Twinpack automatically installs the package, including all depending libraries, on your System and adds it as a reference to your PLC.
9. Follow the library documentation or instructions to incorporate its functionality into your project.


## Share a Package ...

There are multiple ways to publish a package on Twinpack and you can chose how you want to publish your packages depending on how you want to create releases.


### with the Twinpack Registry

The most straight forward way to publish a package, which you release on GitHub is to use the [Twinpack Registry](https://github.com/Zeugwerk/Twinpack-Registry). Create a Pull Request in which you add your repository to the `repositories.txt` file, similarily to this [commit](https://github.com/Zeugwerk/Twinpack-Registry/commit/ecafd41cbc2c97f647bd4512a14d69293f5cc82f). There is a workflow in the twinpack-registry repository, which automatically uploads any new release, which contains a .library file to Twinpack.



### with a GitHub workflow

If you have a CI/CD environment it can be benefical to you to upload your package by using the Twinpack Commandline interface (see the `TwinpackCli` project in Twinpack). For GitHub, we tried to streamline this process as much as possible by providing a GitHub action, which will do this for you, see [twinpack-action](https://github.com/Zeugwerk/twinpack-action) for details.

Note, if you don't have your own CI/CD environment, this [action](https://github.com/Zeugwerk/zkbuild-action) can be used to implement CI/CD on the Zeugwerk CI/CD environment, unlike Twinpack we can only provide this environment free of charge in a limited amount (at the moment 30 buildactions / month)


### manually in your IDE

This is the most straight forward way to publish a package if you want to publish your library directly from your IDE

1. Open a TwinCAT solution and navigate to the PLC library you want to share with the community.
2. Right click the PLC item and click 'Twinpack' -> 'Publish ...'
3. In the dialog, which opens, fill in the information describing your package.
   - Distributor (mandatory): The name you enter here will be exclusively associated with your Twinpack Account, and it will serve as a unique identifier for all your upcoming packages.
   - Version (mandatory): This is the initial version of your package. Once you publish your package for the first time, you have the flexibility to release newer versions in the future.
   - Other fields are optional
4. Click publish to make Twinpack
   - Check all objects of your library
   - Upload the library as a package to the Twinpack server making it available for the community

<p float="left">
<img src="/images/twinpack_contextmenu2.png" width="350" hspace="80" />   
<img src="/images/twinpack_publish.png" width="450" />
</p>

After the initial upload of your library as a package, you may publish newer versions of your library or you can also modify the current version by right clicking on the PLC item and then on 'Twinpack' -> 'Modify...'. Users of Twinpack will be notified whenever a newer version is available in the Twinpack Catalog.


## Further information

🌟 Make sure to follow this project by leaving a star or simply follow us, to always get notified if a newer version of Twinpack is released.

📺 We have also created a short introductionary video on how to install and use Twinpack (more to come):
- [Share TwinCAT libraries with Twinpack](https://youtu.be/xvJG9BRN610?si=RMMIPcdtMAoHkyGW)
