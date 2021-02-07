using System;
using System.Runtime.Serialization;

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

        protected FileOverwriteException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}