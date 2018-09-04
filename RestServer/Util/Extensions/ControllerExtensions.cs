using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MongoDB.Bson;
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
            var exceptionList = new List<Exception>();
            foreach (ModelStateEntry modelState in controller.ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    string message = $"{error.ErrorMessage}: [AttemptedValue={modelState.AttemptedValue}]";
                    var exception = new ValidationException(message, error.Exception);
                    exceptionList.Add(exception);
                }
            }
            if (exceptionList.Count > 0)
            {
                AggregateException agg = new AggregateException(exceptionList);
                throw new ApplicationException("Erro ao realizar o binding do request.", agg);
            }
        }
    }
}
