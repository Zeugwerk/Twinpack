using System;

namespace Twinpack.Exceptions
{
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

    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }

    public class CompileException : Exception
    {
        public CompileException(string message) : base(message)
        {
        }
    }

    public class LoginException : Exception
    {
        public LoginException(string message) : base($"Login failed: {message}")
        {
        }
    }

    public class ChecksumMismatchException : Exception
    {
        public ChecksumMismatchException(string message, string expected, string actual) : base($"{message}, expected={expected}, actual={actual}")
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
