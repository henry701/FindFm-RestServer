using System;
using System.Text.RegularExpressions;
using Models;
using RestServer.Exceptions;

namespace RestServer.Util
{
    internal static class ValidationUtils
    {
        private const string emailPattern = @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?$";
        private static readonly Regex emailRegex = new Regex(emailPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string ValidateEmail(string value)
        {
            if(!emailRegex.IsMatch(value))
            {
                throw new ValidationException("E-mail inválido!");
            }
            return value;
        }

        public static PhoneNumber ValidatePhoneNumber(PhoneNumber value)
        {
            if(value.StateCode == 0)
            {
                throw new ValidationException("Falta o código de área no telefone!");
            }
            return value;
        }

        internal static DateTime ValidateStartDate(DateTime value)
        {
            if (value == default)
            {
                throw new ValidationException("A data deve estar settada!");
            }
            if (value > DateTime.UtcNow)
            {
                throw new ValidationException("Ha ha ha, muito engraçado! A data não pode ser no futuro, Marty McFly!");
            }
            return value.Date;
        }

        public static DateTime ValidateBornDate(DateTime value)
        {
            return ValidateStartDate(value); // Shim
        }

        public static string ValidateName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException("Seu nome não pode ser vazio ou conter apenas caracteres de espaço!");
            }
            return value.Trim();
        }

        public static string ValidatePassword(string value)
        {
            if(String.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException("A senha não pode ser vazia ou conter apenas caracteres de espaço!");
            }
            if(value.Length < 6)
            {
                throw new ValidationException("A senha não pode ter menos de 6 caracteres!");
            }
            return value;
        }
    }
}