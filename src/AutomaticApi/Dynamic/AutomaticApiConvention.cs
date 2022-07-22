using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Linq;
using System.Reflection;

namespace AutomaticApi.Dynamic
{
    public class AutomaticApiConvention : IApplicationModelConvention
    {
        private readonly FieldInfo _actionMethodField = typeof(ActionModel).GetTypeInfo().DeclaredFields.First(o => o.Name == "<ActionMethod>k__BackingField");

        public void Apply(ApplicationModel application)
        {
            foreach (var controllerModel in application.Controllers)
            {
                if (typeof(IAutomaticApi).IsAssignableFrom(controllerModel.ControllerType))
                {
                    var methods = controllerModel.ControllerType.GetInterfaces().SelectMany(o => o.GetTypeInfo().DeclaredMethods).ToDictionary(o => o.ToString());
                    foreach (var actionModel in controllerModel.Actions)
                        _actionMethodField.SetValue(actionModel, methods[actionModel.ActionMethod.ToString()]);
                }
            }
        }
    }
}
