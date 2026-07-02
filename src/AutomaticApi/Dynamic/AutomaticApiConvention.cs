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
                var controllerType = controllerModel.ControllerType;
                if (!typeof(IAutomaticApi).IsAssignableFrom(controllerType))
                    continue;

                var methodMaps = new Dictionary<MethodInfo, MethodInfo>();
                foreach (var item in controllerType.ImplementedInterfaces)
                {
                    // 只有声明了方法的业务接口才有映射价值；跳过 IAutomaticApi 这类标记接口与无方法接口，
                    // 避免 GetInterfaceMap 在无关接口上的反射开销。
                    if (!item.IsInterface || item.GetMethods().Length == 0)
                        continue;

                    var mapping = controllerType.GetInterfaceMap(item);
                    for (int i = 0; i < mapping.InterfaceMethods.Length; i++)
                        methodMaps.TryAdd(mapping.TargetMethods[i], mapping.InterfaceMethods[i]);
                }

                foreach (var actionModel in controllerModel.Actions)
                {
                    // 继承自基类或非接口声明的 action 不在映射表中，跳过而非抛 KeyNotFoundException。
                    if (methodMaps.TryGetValue(actionModel.ActionMethod, out var interfaceMethod))
                        _actionMethodField.SetValue(actionModel, interfaceMethod);
                }
            }
        }
    }
}
