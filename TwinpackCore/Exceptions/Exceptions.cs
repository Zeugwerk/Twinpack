using System;

namespace Twinpack.Exceptions
{
    public abstract class ProjectException : Exception
    {
        public string ProjectName { get; private set; }
        public bool RestartHint { get; private set; }

        public ProjectException(string message) : base(message)
        {
            ProjectName = null;
            RestartHint = false;
        }
        public ProjectException(string projectName, string message, bool restartHint) : base(message)
        {
            ProjectName = projectName;
            RestartHint = restartHint;
        }
    }

    public class CompileException : ProjectException
    {
        public CompileException(string message) : base(message) { }
        public CompileException(string projectName, string message) : base(projectName, (projectName ?? "Solution") + ": " + message, false) { }
    }

    public class AutomationInterfaceUnresponsiveException : ProjectException
    {
        public AutomationInterfaceUnresponsiveException(string message) : base(message) {}
        public AutomationInterfaceUnresponsiveException(string projectName, string message, bool restartHint) : base(projectName, message, restartHint) {}
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

    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
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

    public class PackageServerTypeException : Exception
    {
        public PackageServerTypeException(string message) : base(message)
        {
        }
    }
}
