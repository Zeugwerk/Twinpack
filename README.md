# Twinpack 

The Twinpack Package Manager is a powerful and user-friendly package management tool for TwinCAT libraries. It is designed to empower the TwinCAT community by enabling sharing and distribution of libraries. It acts as a versatile platform similarly to NuGet (but with a PLC touch), allowing users to efficiently manage and deploy their custom-built modules.

---

## Quicklinks
- [Download latest Release](https://github.com/Zeugwerk/Twinpack/releases/latest)
- [Twinpack-Registry](https://github.com/Zeugwerk/Twinpack-Registry) for automatic publishing of your library on Twinpack by "pulling" them from your GitHub releases
- [Registration](https://zeugwerk.dev/wp-login.php?action=register), only needed to "push" package, i.e. if you want to publish packages manually or with CI
- [Contact us](mailto:info@zeugwerk.at)

---

## Installation

To use the Twinpack Package Manager, follow these steps:

1. [Download](https://github.com/Zeugwerk/Twinpack/releases/latest) the latest installer.
2. Select the IDEs, which Twinpack should be installed for.
3. Click on "Install".
4. Follow the on-screen instructions to complete the installation.

## Using a Package

<div style="display: flex; justify-content: space-between;">
<img src="/images/twinpack_catalog.png"/>
</div>


To install a package, follow these steps:

1. Open a TwinCAT solution and navigate to a PLC.
2. Right click the **References** item of your PLC
3. Click **Twinpack Catalog...**
4. Browse or search for the desired package.
5. Click on the package to view details.
6. Click on the **Add** to install this package and add it to the referenced libraries.
7. Wait for the installation process to complete. If you are installing packages from a contributor for the first time and these packages come with a license you will be asked to confirm their license agreement in order to advance.
8. Twinpack automatically installs the package, including all depending libraries, on your System and adds it as a reference to your PLC.
9. Follow the library documentation or instructions to incorporate its functionality into your project.

## Connect to Beckhoff and/or custom package servers

<p float="left">
<img src="/images/twinpack_package_servers.png" />
</p>

Twinpack supports the NuGet protocol for packages, including repositories hosted by Beckhoff since TwinCAT 4026.xx. Follow these steps to add a new repository to Twinpack:

1. Click **Configure** at the top right of the Twinpack Catalog to open the configuration window.
2. Click **Add** to add new package servers.
3. Configure each new package server by:
   - Entering the URL of the package server.
   - Giving it a name of your choice.
   - Selecting the type of package server from the combo box.
4. Depending on the server configuration, you may need to **Login** with your credentials for the respective repository.
5. Click **OK** to save your configuration. The Twinpack catalog will reload and display packages from your additional package servers.

### URLs to popular repositories:

- Official Twinpack Repository (preconfigured): `https://twinpack.dev`. Log in is optional. However, logging in will display your private packages and/or packages that are licensed to you by Zeugwerk. If you forgot your Twinpack login, reset it [here](https://zeugwerk.dev/wp-login.php?action=lostpassword).
- Public Beckhoff Repository: `https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable`, log in in is mandatory. Use your Beckhoff credentials to connect. If you don't have a Beckhoff login, register [here](https://www.beckhoff.com/en-en/mybeckhoff-registration/index.aspx).


## Share a Package

There are multiple ways to publish a package on Twinpack and you can chose how you want to publish your packages depending on how you want to create releases.


### ... with the Twinpack Registry

The most straight forward way to publish a package, which you release on GitHub anyway, is to use the [Twinpack Registry](https://github.com/Zeugwerk/Twinpack-Registry).

- Create a Pull Request in which you add your repository to the `repositories.txt` file, similarily to this [commit](https://github.com/Zeugwerk/Twinpack-Registry/commit/ecafd41cbc2c97f647bd4512a14d69293f5cc82f).
- No other action is needed, there is a workflow in the twinpack-registry repository, which automatically uploads all libraries found in the latest release for all repositories on this list.



### ... with a GitHub workflow

If you have a CI/CD environment it can be benefical to you to upload your package by using the Twinpack Commandline interface (see the `TwinpackCli` project in Twinpack). For GitHub, we tried to streamline this process as much as possible by providing a GitHub action, which will do this for you, see [twinpack-action](https://github.com/Zeugwerk/twinpack-action) for details.

Note, if you don't have your own CI/CD environment, this [action](https://github.com/Zeugwerk/zkbuild-action) can be used to implement CI/CD on the Zeugwerk CI/CD environment, unlike Twinpack we can only provide this environment free of charge in a limited amount (at the moment 30 buildactions / month)


### ... manually in your IDE

This is the most straight forward way to publish a package if you want to publish your library directly from your IDE

1. Open a TwinCAT solution and navigate to the PLC library you want to share with the community.
2. Right click the PLC item and click **Twinpack** -> **Publish ...**
3. In the dialog, which opens, fill in the information describing your package.
   - Distributor (mandatory): The name you enter here will be exclusively associated with your Twinpack Account, and it will serve as a unique identifier for all your upcoming packages.
   - Version (mandatory): This is the initial version of your package. Once you publish your package for the first time, you have the flexibility to release newer versions in the future.
   - Other fields are optional
4. Click publish to make Twinpack
   - Check all objects of your library
   - Upload the library as a package to the Twinpack server making it available for the community

<div style="display: flex; justify-content: center;">
<img src="/images/twinpack_contextmenu2.png" width="270" hspace="80" />   
<img src="/images/twinpack_publish.png" width="370" />
</div>

After the initial upload of your library as a package, you may publish newer versions of your library or you can also modify the current version by right clicking on the PLC item and then on **Twinpack** -> **Modify...**. Users of Twinpack will be notified whenever a newer version is available in the Twinpack Catalog.

## Commandline interface (CLI)

Twinpack provides several commands to manage and configure packages for your projects and PLCs via the commandline rather than the IDE - This is very useful for CI/CD. Note that all commands by default will not use Beckhoff's Automation Interface and instead manipulate files directly. Installing and uninstalling packages on the system is not possible with only file manipulations, instead you have to manually install the downloaded packages via 'RepTool'. However, it is also possible to pass a `--headed` argument to make twinpack perform the actions with the Automation Interface instead, which enables installing and uninstalling as well if needed.
Below is a brief summary of the available commands, along with example usage. For more detailed information, you can always use the `--help` option with any command.

### `config`

Configures or modifies the package source repositories used by Twinpack. The configuration is saved in "%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json", login information is stored in the [Windows Credentials Manager](https://support.microsoft.com/en-us/windows/accessing-credential-manager-1b5c916a-6a16-889f-8581-fc16e8165ac0).

**Example Usage:**
```bash
twinpack.exe config --source https://my-packages.com --type "Beckhoff Repository"
twinpack.exe config --purge --source "https://my-packages.com" --type "NuGet Repository" --username "MyUsername" --password "MyPassword"
twinpack.exe config --reset
```

### `search`
Searches packages in all configured package sources

**Example Usage:**
```bash
twinpack.exe search
twinpack.exe search MySearchTerm
```

### `list`
Lists packages configured in the .Zeugwerk/config.json file or the first solution found in the current directory.

**Example Usage:**
```bash
twinpack.exe list --plc MainPLC
twinpack.exe list MySearchTerm
```

### `download`
Downloads package(s) from the configured sources to your system.

**Example Usage:**
```bash
twinpack.exe download --package PackageName
twinpack.exe download --package PackageName --version 2.3.4
```

### `set-version`

Sets the version of the PLC(s). If a PLC is part of a larger framework, the command can also be used to update all packages, which are part of the same framework.

**Example Usage:**
```bash
twinpack.exe set-version 1.1.0
twinpack.exe set-version 1.1.0 --plc MyPlc
twinpack.exe set-version 1.1.0 --plc MyPlc --sync-framework-packages
```


### `add`

Adds package(s) to a specified project and PLC using the configured sources. The command downloads, installs and adds the packages to the PLĆ's references - including dependencies if requested

**Example Usage:**
```bash
twinpack.exe add --project MyProject --plc MainPLC --package PackageName
twinpack.exe add --project MyProject --plc MainPLC --package PackageName --version 1.0.0
```

### `remove`
Removes package(s) from a specified project and PLC.

**Example Usage:**
```bash
twinpack.exe remove --project MyProject --plc MainPLC --package PackageName
twinpack.exe remove --project MyProject --plc MainPLC
```

### `help`
For detailed information about each command and its available options, you can use the --help argument:

**Example Usage:**
```bash
twinpack.exe <command> --help
```


## Further information

🌟 Make sure to follow this project by leaving a star or simply follow us, to always get notified if a newer version of Twinpack is released.

📺 We have also created a short introductionary video on how to install and use Twinpack
- [Share TwinCAT libraries with Twinpack](https://youtu.be/xvJG9BRN610?si=RMMIPcdtMAoHkyGW)
