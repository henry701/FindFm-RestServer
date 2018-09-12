using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestServer.Exceptions;

namespace RestServer.Util.Extensions
{
    internal static class ControllerExtensions
    {
        public static string GetCurrentUserId(this ControllerBase controller)
        {
            return controller.User.Identity.Name;
        }

        public static void EnsureModelValidation(this ControllerBase controller)
        {
            if (controller.ModelState.IsValid)
            {
                return;
            }
            var errorList = new List<string>();
            foreach (ModelStateEntry modelState in controller.ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    string message = $"{error.ErrorMessage} (Valor Enviado: {modelState.AttemptedValue})";
                    errorList.Add(message);
                }
            }
            if (errorList.Count > 0)
            {
                throw new ValidationException("Erro ao realizar o binding do request: " +
                    errorList.Aggregate("", (s1, s2) => $"{s1}, {s2}"));
            }
        }
    }
}
