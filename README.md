# Twinpack 

The Twinpack Package Manager is a powerful and user-friendly package management tool for TwinCAT libraries. It is designed to empower the TwinCAT community by enabling sharing and distribution of libraries. It acts as a versatile platform similarly to NuGet (but with a PLC touch), allowing users to efficiently manage and deploy their custom-built modules.

Twinpack is free. It is developed by [Zeugwerk](https://zeugwerk.at) and is the standard way to install and update [Zeugwerk Framework](https://doc.zeugwerk.dev/framework/overview.html) - but it works for any TwinCAT library from any publisher.


<div style="display: flex; justify-content: space-between;">
<img src="/images/twinpack_catalog.png"/>
</div>

Twinpack currently supports the following package sources

1. [Twinpack server](https://doc.zeugwerk.dev/twinpack/twinpack_quickstart.html#share-a-package-): Zeugwerk hosts open source libraries for and from everyone who is interested for free. Additionally this server type supports special features for Zeugwerk customers like feature branches.
1. [Nuget Server](https://doc.zeugwerk.dev/twinpack/twinpack_nuget_package.html): Everyone can host his own NuGet server and create packages to consume them from the on premises server.
1. [Beckhoff Library Repository](https://doc.zeugwerk.dev/twinpack/twinpack_beckhoffrepository.html): Since TwinCAT 4026, Beckhoff provides a public repository for their libraries. Twinpack can connect this repositories and integrate them seamlessly into the IDE.

The full project documentation, including a quickstart guide for **Twinpack**, is available at the following at [Project Documentation](https://zeugwerk.dev/Zeugwerk_Framework/Documentation/release/1.6/twinpack/twinpack_quickstart.html)

Visit the link to get detailed instructions on setting up and using the project.

## Quicklinks
- [Download latest Release](https://github.com/Zeugwerk/Twinpack/releases/latest)
- [Twinpack-Registry](https://github.com/Zeugwerk/Twinpack-Registry) for automatic publishing of your library on Twinpack by "pulling" them from your GitHub releases
- [Registration](https://zeugwerk.dev/wp-login.php?action=register), only needed to "push" package, i.e. if you want to publish packages manually or with CI
- [Contact us](mailto:info@zeugwerk.at)

## Zeugwerk Ecosystem

- [Zeugwerk Framework](https://github.com/Zeugwerk/Zeugwerk-Framework): OOP framework for TwinCAT (install via Twinpack).
- [Development Kit](https://github.com/Zeugwerk/Zeugwerk-Development-Kit/releases/latest): Trial to get started.

## Further information

🌟 Make sure to follow this project by leaving a star or simply follow us, to always get notified if a newer version of Twinpack is released.

📺 We have also created a short introductionary video on how to install and use Twinpack
- [Share TwinCAT libraries with Twinpack](https://youtu.be/xvJG9BRN610?si=RMMIPcdtMAoHkyGW)
