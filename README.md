# Zeugwerk-Twinpack

This is the home of the Twinpack, a lightweight standalone package manager for TwinCAT libraries.


## Twinpack Server

The API of a Twinpack Server implements the following endpoints

### [POST] /twinpack.php?package
This is used to submit a package to the Twinpack server. The request should contain a JSON object, which has the following fields

```json
{
  "name": "struckig",
  "description": "...",
  "authors": "",                             // optional, defaults to ""
  "entitlement": "",                         // optional, defaults to ""

  "version": "1.1.1.2",
  "binary": "...",
  "license": "MIT",
  "branch": "main",                          // optional, defaults to "main"
  "released": 1,                             // optional, defaults to 1
  "notes": "Some small thing has changed",   // optional, defaults to ""
  "license-binary": "...",                   // optional, defaults to ""
}
```

Additionally you have to pass the user credentials for Zeugwerk CI in the header in the fields, `ZGWK-USERNAME` `ZGWK-PASSWORD`.

Note that the fields `authors`, `description` and `entitlement` are used for the package description as a whole, while the other fields are specific to the `version`, which is currently uploaded.
The combination of `ZGWK-USERNAME`, `name` and `version` has to be unique as this API does not allow to overwrite an already existing package.

 
### [GET] /twinpack.php?package
Use this endpoint to download a specific package. You can use the catalog and product-version APIs to find the package identifier. The request takes a JSON object, which contains the following fields.

- *id*: Unique identifier of the package to retrieve

The API returns the a JSON object that contains the relevant information of the package (Base64 encoded binary of the library file, the license file, and so on).
```json
{
    "id": 4,
    "repository": "<repository_name>",
    "entitlement": null
    "binary": "...",
    "license": "MIT",
    "license-binary": "...",
    "released": 1,
}
```

### [GET] /twinpack.php?catalog
The API returns the a JSON object that contains packages that are available on the Twinpack Server

```json
[
    {
        "name": "struckig",
        "repository": "<repository_name>",
        "entitlement": null,
        "description": "<short description of the package>",
        "versions": "3",
        "created": "2023-07-04 08:41:23",
        "modified": "2023-07-04 08:44:49"
    },
    {
        "name": "TcUnit",
        "repository": "<repository_name>",
        "entitlement": null,
        "description": "<short description of the package>",
        "versions": "2",
        "created": "2023-07-04 08:59:56",
        "modified": "2023-07-04 09:35:08"
    },
    ...
]
```


### [GET] /twinpack.php?package-versions
Use this API to get more information about a specific package by its repository and name. The request should look like

```json
[
  {
      "repository": "bot",
      "name": "struckig"
  }
]
```

and it response with a list of all available versions of the requested package
  
```json
[
    {
        "id": 1,
        "name": "struckig",
        "branch": "main",
        "version": "0.9.0.1",
        "released": 1,
        "repository": "bot",
        "authors": "Stefan Besler",
        "license": "MIT"
    },
    {
        "id": 2,
        "name": "struckig",
        "branch": "main",
        "version": "0.10.0.1",
        "released": 0,
        "repository": "bot",
        "authors": null,
        "license": "MIT"
    },
    ...
]
```

You can use the `id` to download a specific package with the `package` API.