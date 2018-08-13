using System;
using System.Runtime.Serialization;

namespace RestServer.Exceptions
{
    [Serializable]
    internal class ValidationException : ApplicationException
    {
        public ValidationException()
        {

        }

        public ValidationException(string message) : base(message)
        {

        }

        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {

        }

        protected ValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }
    }
}