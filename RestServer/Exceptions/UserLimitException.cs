using System;
using System.Runtime.Serialization;

namespace RestServer.Exceptions
{
    [Serializable]
    internal class UserLimitException : ApplicationException
    {
        public UserLimitException()
        {

        }

        public UserLimitException(string message) : base(message)
        {

        }

        public UserLimitException(string message, Exception innerException) : base(message, innerException)
        {

        }

        protected UserLimitException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }
    }
}