using System;

namespace Twinpack.Exceptions
{
    public class DependencyNotFoundException : Exception
    {
        public string Reference { get; private set; }
        public string Version { get; private set; }
        public DependencyNotFoundException(string reference, string version, string message) : base(message)
        {
            Reference = reference;
            Version = version;
        }
    }

    public class DependencyAddException : Exception
    {
        public string Reference { get; private set; }
        public string Version { get; private set; }
        public DependencyAddException(string reference, string version, string message) : base(message)
        {
            Reference = reference;
            Version = version;
        }
    }

    public class LibraryNotFoundException : Exception
    {
        public string Reference { get; private set; }
        public string Version { get; private set; }
        public LibraryNotFoundException(string reference, string version, string message) : base(message)
        {
            Reference = reference;
            Version = version;
        }
    }

    public class LibraryInvalid : Exception
    {
        public LibraryInvalid(string message) : base(message)
        {
        }

        public LibraryInvalid(string fileName, string message) : base(fileName + ", " + message)
        {
        }
    }

    public class LicenseFileNotFoundException : Exception
    {
        public string Reference { get; private set; }
        public string Version { get; private set; }
        public LicenseFileNotFoundException(string reference, string version, string message) : base(message)
        {
            Reference = reference;
            Version = version;
        }
    }

    public class PostException : Exception
    {
        public PostException(string message) : base(message)
        {
        }
    }

    public class GetException : Exception
    {
        public GetException(string message) : base(message)
        {
        }
    }

    public class PutException : Exception
    {
        public PutException(string message) : base(message)
        {
        }
    }

    public class QueryException : Exception
    {
        public QueryException(string query, string message) : base($"Query {query} failed: {message}")
        {
        }
    }

    public class LoginException : Exception
    {
        public LoginException(string message) : base($"Login failed: {message}")
        {
        }
    }

    public class LicenseDeclined : Exception
    {
        public LicenseDeclined(string message) : base(message)
        {
        }
    }

    public class PushException : Exception
    {
        public PushException(string message) : base(message)
        {
        }
    }

    public class ChecksumException : Exception
    {
        public ChecksumException(string message, string expected, string actual) : base($"{message}, expected={expected}, actual={actual}")
        {
        }
    }

    public class LibraryFileInvalidException : Exception
    {
        public LibraryFileInvalidException(string message) : base(message)
        {
        }
    }

    public class PackageServerTypeNotFoundException : Exception
    {
        public PackageServerTypeNotFoundException(string message) : base(message)
        {
        }
    }
    
}
