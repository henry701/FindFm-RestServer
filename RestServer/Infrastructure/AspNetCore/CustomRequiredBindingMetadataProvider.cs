using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace RestServer.Infrastructure.AspNetCore
{
    internal sealed class CustomRequiredBindingMetadataProvider : IBindingMetadataProvider
    {
        public void CreateBindingMetadata(BindingMetadataProviderContext context)
        {
            if (context.PropertyAttributes != null && context.PropertyAttributes.OfType<RequiredAttribute>().Any())
            {
                context.BindingMetadata.IsBindingRequired = true;
            }
        }
    }
}
