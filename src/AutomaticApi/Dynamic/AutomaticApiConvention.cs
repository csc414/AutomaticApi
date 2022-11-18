using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Collections.Generic;
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
                    var methodMaps = new Dictionary<MethodInfo, MethodInfo>();
                    foreach (var item in controllerModel.ControllerType.ImplementedInterfaces)
                    {
                        var mapping = controllerModel.ControllerType.GetInterfaceMap(item);
                        for (int i = 0; i < mapping.InterfaceMethods.Length; i++)
                            methodMaps.TryAdd(mapping.TargetMethods[i], mapping.InterfaceMethods[i]);
                    }
                    foreach (var actionModel in controllerModel.Actions)
                        _actionMethodField.SetValue(actionModel, methodMaps[actionModel.ActionMethod]);
                }
            }
        }
    }
}
