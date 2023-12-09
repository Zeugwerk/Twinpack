# Library reader

Twinpack Registry extract project informations out of CoDeSyS Libraries.
The fileformat is closed, so some tinkering was required to find out how the fileformat
is structured. This page summaries our findings, which are implemented in 
[LibraryReader.cs](https://github.com/Zeugwerk/Twinpack/blob/main/TwinpackShared/LibraryReader.cs).

## .library file

A .library file is a ZIP Archive, so any unzip tool can be used to extract
the file. In Windows, without anz other tools, the extension of said files
can simply be changed to .zip and then viewed with the Windows Explorer.
Some Linux desktop enviornment even directly recognize the format and allow
direct navigation.

### String table

Every archive contains a file called "__shared_data_storage_string_table__.auxiliary",
which holds all commonly used strings by other files. The first byte contains the count
of strings in the file. "Lengths" in general in the file are variable length encoded:

- If Bit 8 of the Byte "b1" is unset, "b1" directly contains the length information
- If Bit 8 of the Byte "b1" is set (this mean it is greater or equal 128), the following byte "b2" has to be read as well
  and length = (b1 - 128) * 128 * b2

What follows after the initial count is a list of strings, which are encoded as a sequence of length and data. This means that the decode any
string, one has continuoiusly read a length "len" (as described above) followed by reading len * bytes, which us the ascii encoded content of any string in the file.
This has the be repeated until all strings are decoded.


### Project information (>= 4024.53)

Starting from TwinCAT 4024.53 (maybe on or two releases before as well), project information, such as

- Title
- Version
- Default namespace
- Library categories
- ...

are stored in a XML file called "projectinformations.auxiliary". Since this is plain text, it should be obvious how to parse the information.


### Project information (< 4024.53)

Before, the information was stored in a, much harded ro decode, binary format called 
"11c0fc3a-9bcf-4dd8-ac38-efb93363e521.object". This file is structured as follows:

- The first 16 byte contain a file header
- Then a "length" contains the informatio about how many bytes follow until the end of file is reached.
- Then there are 7 bytes that are not directly related to the project information.
- What follows is a list of key value pairs, which we call properties, which are encoded like this:
  - 1 Byte the holds the index to the string in the string table file, this is the "key" of the property (e.g. Title)
  - 1 Byte that comtains the type of the property
    - 0x01: Boolean
      - Read 1 more byte the get the value of the property (true or false)
    - 0x0E: Text
      - Read one more byte to get an index to a string in the string table, which in turn is the value of the property
    - 0x0F: Version
      - Read 1 byte, which contains unknown information
      - Read 1 byte, which is again an index in the string table, which holds the version number information
    - 0x81: Library categories
      - Read 1 byte to get an index to a "GUID" in the string table
      - Read 1 byte to get the "count" of selected categories
      - For "GUID=System.Guid", this property holds the information about the library categories, which
        are "selected" for the library.
        - Read 2 byte of unknown data
        - Read "count" bytes to get the guids of the selected categories
      - If the text is not "System.Guid"
        - Read 4 bytes for some kind of header
        - Read "(count-1)*7+4" bytes to get the information about all library categories
          - Default name
          - Some number
          - Some number
          - Version
          - Some number (only for count-1 categories)
          - Guid (only for count-1 categories, seems like some kind of linked list)
          - Guid (only for count-1 categories(
