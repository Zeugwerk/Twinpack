# Zeugwerk-Twinpack

This is the home of the Twinpack, a lightweight standalone package manager for TwinCAT libraries.

## Installation

## Find and install a package

## Update a package

## Upload a package

## Twinpack Server API

The API of a Twinpack Server implements the following endpoints

### [POST] /twinpack.php?controller=package
This is used to submit a package to the Twinpack server. The request should contain a JSON object, which has the following fields

```json
{
  "name": "struckig",
  "description": "...",
  "authors": "",
  "entitlement": "",

  "version": "1.1.1.2",
  "configuration": "Release",
  "target": "TC3.1",
  "binary": "...",
  "license": "MIT",
  "branch": "main",
  "compiled": 0,
  "notes": "Some small thing has changed",
  "license-binary": "...",
}
```

Additionally you have to pass the user credentials for Zeugwerk CI in the header in the fields, `ZGWK-USERNAME` `ZGWK-PASSWORD`.

Note that the fields `authors`, `description` and `entitlement` are used for the package description as a whole, while the other fields are specific to a specific iteration of the package.
The combination of `ZGWK-USERNAME`, `name`, `version`, `branch`, `target`, `configuration` has to be unique as this API does not allow to overwrite an already released packages (e.g. where the configuration is "Release").

 
### [GET] /twinpack.php?controller=package

#### Parameters
- repository
- name
- target
- configuration (optional), defaults to Release
- branch (optional), defaults to main
- version

or

- id: Unique identifier of the package to retrieve

#### Description
Use this endpoint to download one specific version of a package. You can use the catalog and product-version APIs to find the package identifier. The request takes a JSON object, which contains the following fields.

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
    "compiled": 0
}
```

### [GET] /twinpack.php?controller=catalog
#### Parameters
- search (optional)
- per_page (optional) for pagination, limits the number of returned results
- page (optional) for pagination, what page to show. Note the API call returns a HTTP Header "Link", which contains a JSON object that can be used for navigation between pages, i.e. the JSON object contained in the link header has fields for "prev" (URL to the previous page) and "next" (URL to the next page). If "next" or "prev" is null, the end of the start of has been reached, respectively.

#### Description
The API returns the a JSON object that shows packages that are available on the Twinpack Server. Note that the results differ depending on the user, which calls the API. Publically, only
results are returned, whose "configuration" is "Release". You can use the "ZGWK-USERNAME" and "ZGWK-PASSWORD" header to return all packages that are available to you.

```json
[
    {
        "name": "struckig",
        "repository": "<repository_name>",
        "entitlement": null,
        "description": "<short description of the package>",
        "versions": 3,
        "configuration" : 3,
        "targets": 1,
        "downloads": 5
        "created": "2023-07-04 08:41:23",
        "modified": "2023-07-04 08:44:49"
    },
    {
        "name": "TcUnit",
        "repository": "<repository_name>",
        "entitlement": null,
        "description": "<short description of the package>",
        "versions": 2,
        "configuration" : 3,
        "targets": 1,
        "downloads": 10,
        "created": "2023-07-04 08:59:56",
        "modified": "2023-07-04 09:35:08"
    },
]
```

### [GET] /twinpack.php?controller=package-version
#### Parameters
- repository
- name (optional) 
- branch (optional), defaults to main
- target (optional), defaults to TC3.1
- version (optional)
- configuration (optional)
- per_page (optional) for pagination, limits the number of returned results
- page (optional) for pagination, what page to show. Note the API call returns a HTTP Header "Link", which contains a JSON object that can be used for navigation between pages, i.e. the JSON object contained in the link header has fields for "prev" (URL to the previous page) and "next" (URL to the next page). If "next" or "prev" is null, the end of the start of has been reached, respectively.

#### Description
Use this API to get more information about a specific package by its repository and name. The additional version parameter is optional and can be used to check if a package already exists on the server. Note that the results differ depending on the user, which calls the API. Publically, only
results are returned, whose "configuration" is "Release". You can use the "ZGWK-USERNAME" and "ZGWK-PASSWORD" header to return all packages that are available to you. The response of the request looks like this
  
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
]
```

You can use the `id` to download a specific package with the `package` API.
