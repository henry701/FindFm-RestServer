using System;
using System.Collections.Generic;
using System.Text;

namespace RestServer.Util.Extensions
{
    internal static class TypeExtensions
    {
        public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                foreach(var interf in toCheck.GetInterfaces())
                {
                    if(interf.IsSubclassOfRawGeneric(generic))
                    {
                        return true;
                    }
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }
    }
}
