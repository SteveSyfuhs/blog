using System;

namespace blog
{
    [Serializable]
    internal class FileOverwriteException : Exception
    {
        public FileOverwriteException()
        {
        }

        public FileOverwriteException(string message) : base(message)
        {
        }

        public FileOverwriteException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}