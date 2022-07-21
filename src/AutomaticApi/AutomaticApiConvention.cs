using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace AutomaticApi
{
    public class AutomaticApiConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controllerModel in application.Controllers)
            {
                if (typeof(IAutomaticApi).IsAssignableFrom(controllerModel.ControllerType))
                {
                    var methods = controllerModel.ControllerType.GetInterfaces().SelectMany(o => o.GetTypeInfo().DeclaredMethods).ToDictionary(o => o.ToString());
                    foreach (var actionModel in controllerModel.Actions)
                    {
                        var field = typeof(ActionModel).GetTypeInfo().DeclaredFields.First(o => o.Name == "<ActionMethod>k__BackingField");
                        field.SetValue(actionModel, methods[actionModel.ActionMethod.ToString()]);
                    }
                }
            }
        }
    }
}
