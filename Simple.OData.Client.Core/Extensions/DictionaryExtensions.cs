﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simple.OData.Client.Extensions
{
    internal static class DictionaryExtensions
    {
        private static readonly Dictionary<Type, ConstructorInfo> _constructors = new Dictionary<Type, ConstructorInfo>(); 

        public static T ToObject<T>(this IDictionary<string, object> source)
            where T : class
        {
            if (source == null)
                return default(T);

            var value = CreateInstance<T>();
            var type = value.GetType();
            return (T)ToObject(source, type, value);
        }

        public static object ToObject(this IDictionary<string, object> source, Type type, object value = null)
        {
            if (source == null)
                return null;

            if (value == null)
            {
                var defaultConstructor = type.GetConstructor(new Type[] {});
                if (defaultConstructor != null)
                {
                    value = defaultConstructor.Invoke(new object[] { });
                }
            }

            Func<Type, bool> IsCompoundType = fieldOrPropertyType =>
            {
                return !fieldOrPropertyType.IsValueType && !fieldOrPropertyType.IsArray && fieldOrPropertyType != typeof(string);
            };

            Func<Type, object, bool> IsCollectionType = (fieldOrPropertyType, itemValue) =>
            {
                return fieldOrPropertyType.IsArray && (itemValue as System.Collections.IEnumerable) != null;
            };

            Func<Type, object, object> ConvertSingle = (fieldOrPropertyType, itemValue) =>
            {
                return IsCompoundType(fieldOrPropertyType)
                    ? (itemValue as IDictionary<string, object>).ToObject(fieldOrPropertyType)
                    : itemValue;
            };

            Func<Type, object, object> ConvertCollection = (fieldOrPropertyType, itemValue) =>
            {
                var elementType = fieldOrPropertyType.GetElementType();
                var count = 0;
                foreach (var v in (itemValue as System.Collections.IEnumerable)) count++;
                var arrayValue = Array.CreateInstance(elementType, count);

                count = 0;
                foreach (var item in (itemValue as System.Collections.IEnumerable))
                {
                    (arrayValue as Array).SetValue(ConvertSingle(elementType, item), count++);
                }
                return arrayValue;
            };

            Func<Type, object, object> ConvertValue = (fieldOrPropertyType, itemValue) =>
            {
                return IsCollectionType(fieldOrPropertyType, itemValue)
                            ? ConvertCollection(fieldOrPropertyType, itemValue)
                            : ConvertSingle(fieldOrPropertyType, itemValue);
            };

            foreach (var item in source)
            {
                if (item.Value != null)
                {
                    var property = type.GetProperty(item.Key);
                    if (property != null)
                    {
                        property.SetValue(value, ConvertValue(property.PropertyType, item.Value), null);
                    }
                }
            }

            return value;
        }

        public static IDictionary<string, object> ToDictionary(this object source,
            BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            if (source == null)
                return new Dictionary<string, object>();

            return source.GetType().GetProperties(bindingAttr).ToDictionary
            (
                propInfo => propInfo.Name,
                propInfo => propInfo.GetValue(source, null)
            );

        }

        private static T CreateInstance<T>()
            where T : class
        {
            ConstructorInfo ctor = null;
            
            if (!_constructors.TryGetValue(typeof(T), out ctor))
            {
                if (typeof(T) == typeof(IDictionary<string, object>))
                {
                    return new Dictionary<string, object>() as T;
                }
                else
                {
                    ctor = typeof(T).GetConstructor(new Type[] { });
                    lock (_constructors)
                    {
                        if (!_constructors.ContainsKey(typeof(T)))
                            _constructors.Add(typeof(T), ctor);
                    }
                }
            }

            if (ctor == null)
            {
                throw new InvalidOperationException(
                    string.Format("Unable to create an instance of type {0} that does not have a default constructor."));
            }

            return ctor.Invoke(new object[] { }) as T;
        }
    }
}
