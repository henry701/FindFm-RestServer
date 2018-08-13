using System;
using RestServer.Exceptions;

namespace RestServer.Util
{
    internal static class ValidationUtils
    {
        internal static string ValidateEmail(string value)
        {
            return value;
        }

        internal static DateTime ValidateBornDate(DateTime value)
        {
            return value;
        }

        internal static string ValidateName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException("Seu nome não pode ser vazio ou conter apenas caracteres de espaço!");
            }
            return value;
        }

        internal static string ValidatePassword(string value)
        {
            if(String.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException("A value não pode ser vazia ou conter apenas caracteres de espaço!");
            }
            return value;
        }
    }
}