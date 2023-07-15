using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
