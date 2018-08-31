﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace RestServer.Util.Extensions
{
    internal static class EnumExtensions
    {
        public static TAttribute GetAttribute<TAttribute>(this Enum enumValue) where TAttribute : Attribute
        {
            Type enumType = enumValue.GetType();
            Type attributeType = typeof(TAttribute);
            return enumType.GetMember(Enum.GetName(enumType, enumValue)).First().GetCustomAttributes(attributeType, true).OfType<TAttribute>().FirstOrDefault();
        }

        public static TEnum FromDisplayName<TEnum>(string displayName) where TEnum : Enum 
        {
            Type enumType = typeof(TEnum);
            Array enumValues = enumType.GetEnumValues();
            foreach(object enumVal in enumValues)
            {
                TEnum enumInstance = (TEnum) enumVal;
                DisplayAttribute displayAttr = enumInstance.GetAttribute<DisplayAttribute>();
                if(displayAttr == null)
                {
                    continue;
                }
                if (String.Equals(displayAttr.Name, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return enumInstance;
                }
            }
            return default;
        }

        // TODO refactor with above
        public static TEnum FromShortDisplayName<TEnum>(string displayName) where TEnum : Enum
        {
            Type enumType = typeof(TEnum);
            Array enumValues = enumType.GetEnumValues();
            foreach (object enumVal in enumValues)
            {
                TEnum enumInstance = (TEnum)enumVal;
                DisplayAttribute displayAttr = enumInstance.GetAttribute<DisplayAttribute>();
                if (displayAttr == null)
                {
                    continue;
                }
                if (String.Equals(displayAttr.ShortName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return enumInstance;
                }
            }
            return default;
        }
    }
}
