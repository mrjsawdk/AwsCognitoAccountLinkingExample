using System;
using System.Runtime.Serialization;

namespace CognitoTriggers.Exceptions
{
    [Serializable]
    public class PreSignupTriggerException : Exception
    {
        public PreSignupTriggerException()
        {
        }

        protected PreSignupTriggerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public PreSignupTriggerException(string message) : base(message)
        {
        }

        public PreSignupTriggerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
