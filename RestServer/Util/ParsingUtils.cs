using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Models;

namespace RestServer.Util
{
    internal static class ParsingUtils
    {
        private static readonly string phonePattern =
            @"
            ^                                   # Assert that position = beginning of string
  
            \s*                                 # Any Whitespace

            (?:
                [+]?                            # Optionally literally match '+'
                \s*                             # Any Whitespace
                (?<CountryCode>
                    \d{1,2}
                )
            )?

            \s*                                 # Any Whitespace

            (?:
                (?:
                    \(                          # Literally match '('
                        \s*                     # Any Whitespace
                        (?<AreaCode>
                            \d{1,2}             # One or two digits
                        )
                        \s*                     # Any Whitespace
                    \)                          # Literally match ')'
                )
                |                               # Or
                (?:
                    (?<AreaCode>
                        \d{1,2}                 # One or two digits
                    )
                )
            )?
            
            \s*                                 # Any Whitespace

            (?<Number0>
                \d?
            )

            \s*                                 # Any Whitespace

            (?<Number1>
                \d{4}
            )
                -?
            (?<Number2>
                \d{4}
            )

            \s*                                 # Any Whitespace

            $                                   # Assert that position = end of string
            ";
        private static readonly Regex phoneRegex = new Regex(phonePattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static PhoneNumber ParsePhoneNumber(string phone)
        {
            var groups = phoneRegex.Match(phone).Groups;

            var countryCodeVal = groups["CountryCode"].Value;
            var areaCodeVal = groups["AreaCode"].Value;
            var number0Val = groups["Number0"].Value;
            var number1Val = groups["Number1"].Value;
            var number2Val = groups["Number2"].Value;

            // Country code without plus, but was actually area code
            if(!string.IsNullOrWhiteSpace(countryCodeVal) && string.IsNullOrWhiteSpace(areaCodeVal))
            {
                areaCodeVal = countryCodeVal;
                countryCodeVal = "";
            }

            if(string.IsNullOrWhiteSpace(countryCodeVal))
            {
                countryCodeVal = "55";
            }

            if(string.IsNullOrWhiteSpace(areaCodeVal))
            {
                throw new ValidationException("Código de área do telefone ausente ou inválido!");
            }

            if (string.IsNullOrWhiteSpace(number1Val) || string.IsNullOrWhiteSpace(number2Val))
            {
                throw new ValidationException("Número do telefone ausente ou inválido!");
            }

            return new PhoneNumber()
            {
                CountryCode = ((PhoneCountry) Convert.ToInt32(countryCodeVal)),
                Number = Convert.ToString(number0Val + number1Val + number2Val),
                StateCode = (PhoneRegion) Convert.ToInt32(areaCodeVal),
            };
        }
    }
}
