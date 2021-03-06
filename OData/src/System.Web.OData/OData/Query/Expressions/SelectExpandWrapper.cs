﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Web.Http;
using System.Web.OData.Formatter;
using System.Web.OData.Formatter.Serialization;
using System.Web.OData.Properties;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Newtonsoft.Json;

namespace System.Web.OData.Query.Expressions
{
    /// <summary>
    /// Represents a container class that contains properties that are either selected or expanded using $select and $expand.
    /// </summary>
    /// <typeparam name="TElement">The element being selected and expanded.</typeparam>
    [JsonConverter(typeof(SelectExpandWrapperConverter))]
    internal class SelectExpandWrapper<TElement> : IEdmEntityObject, ISelectExpandWrapper
    {
        private static readonly IPropertyMapper DefaultPropertyMapper = new IdentityPropertyMapper();
        private static readonly Func<IEdmModel, IEdmStructuredType, IPropertyMapper> _mapperProvider =
            (IEdmModel m, IEdmStructuredType t) => DefaultPropertyMapper;

        private Dictionary<string, object> _containerDict;
        private TypedEdmEntityObject _typedEdmEntityObject;

        /// <summary>
        /// Gets or sets the instance of the element being selected and expanded.
        /// </summary>
        public TElement Instance { get; set; }

        /// <summary>
        /// An ID to uniquely identify the model in the <see cref="ModelContainer"/>.
        /// </summary>
        public string ModelID { get; set; }

        /// <summary>
        /// Gets or sets the EDM type name of the element being selected and expanded. 
        /// </summary>
        /// <remarks>This is required by the <see cref="ODataMediaTypeFormatter"/> during serialization. If the instance property is not
        /// null, the type name will not be set as the type name can be figured from the instance runtime type.</remarks>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the property container that contains the properties being expanded. 
        /// </summary>
        public PropertyContainer Container { get; set; }

        /// <inheritdoc />
        public IEdmTypeReference GetEdmType()
        {
            IEdmModel model = GetModel();

            if (TypeName != null)
            {
                IEdmEntityType entityType = model.FindDeclaredType(TypeName) as IEdmEntityType;
                if (entityType == null)
                {
                    throw Error.InvalidOperation(SRResources.EntityTypeNotInModel, TypeName);
                }

                return new EdmEntityTypeReference(entityType, isNullable: false);
            }
            else
            {
                Type elementType = GetElementType();
                return model.GetEdmTypeReference(elementType);
            }
        }

        /// <inheritdoc />
        public bool TryGetPropertyValue(string propertyName, out object value)
        {
            // look into the container first to see if it has that property. container would have it 
            // if the property was expanded.
            if (Container != null)
            {
                _containerDict = _containerDict ?? Container.ToDictionary(DefaultPropertyMapper, includeAutoSelected: true);
                if (_containerDict.TryGetValue(propertyName, out value))
                {
                    return true;
                }
            }

            // fall back to the instance.
            if (Instance != null)
            {
                _typedEdmEntityObject = _typedEdmEntityObject ??
                    new TypedEdmEntityObject(Instance, GetEdmType() as IEdmEntityTypeReference, GetModel());

                return _typedEdmEntityObject.TryGetPropertyValue(propertyName, out value);
            }

            value = null;
            return false;
        }

        public IDictionary<string, object> ToDictionary()
        {
            return ToDictionary(_mapperProvider);
        }
        private bool IsStructuralOrComplex(IEdmProperty property)
        {
            bool result = false;
            switch(property.PropertyKind)
            {
                case EdmPropertyKind.Structural:
                    result = true;
                    break;
                case EdmPropertyKind.Navigation:
                    {
                        IEdmType propertyDefinition = property.Type.Definition;
                        switch (propertyDefinition.TypeKind)
                        {
                            case EdmTypeKind.Collection:
                                break;
                            case EdmTypeKind.Entity:
                                {
                                    var edmEntityType = propertyDefinition as IEdmEntityType;

                                    while(edmEntityType!=null)
                                    {
                                        if (edmEntityType.DeclaredKey != null)
                                        {
                                            break;
                                        }

                                        if (edmEntityType.BaseType == null)
                                        {
                                            result = true;//Complex Type
                                            break;
                                        }
                                            
                                        edmEntityType = edmEntityType.BaseType as IEdmEntityType;
                                    }
                                }
                                break;
                            case EdmTypeKind.Complex:
                                result = true;
                                break;
                            default://Cannot be complex
                                break;
                        }
                    }
                    break;
                case EdmPropertyKind.None:
                    break;
            }
            return result;
        }
        public IDictionary<string, object> ToDictionary(Func<IEdmModel, IEdmStructuredType, IPropertyMapper> mapperProvider)
        {
            if (mapperProvider == null)
            {
                throw Error.ArgumentNull("mapperProvider");
            }

            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            IEdmStructuredType type = GetEdmType().AsStructured().StructuredDefinition();

            IPropertyMapper mapper = mapperProvider(GetModel(), type);
            if (mapper == null)
            {
                throw Error.InvalidOperation(SRResources.InvalidPropertyMapper, typeof(IPropertyMapper).FullName,
                    type.FullTypeName());
            }

            if (Container != null)
            {
                dictionary = Container.ToDictionary(mapper, includeAutoSelected: false);
            }

            // The user asked for all the structural properties on this instance.
            if (Instance != null)
            {
                foreach (IEdmProperty property in type.Properties())
                {
                    if (IsStructuralOrComplex(property))
                    {
                        object propertyValue;
                        if (TryGetPropertyValue(property.Name, out propertyValue))
                        {
                            string mappingName = mapper.MapProperty(property.Name);
                            if (String.IsNullOrWhiteSpace(mappingName))
                            {
                                throw Error.InvalidOperation(SRResources.InvalidPropertyMapping, property.Name);
                            }

                            dictionary[mappingName] = propertyValue;
                        }
                    }
                }
            }

            return dictionary;
        }

        private Type GetElementType()
        {
            return Instance == null ? typeof(TElement) : Instance.GetType();
        }

        private IEdmModel GetModel()
        {
            Contract.Assert(ModelID != null);

            return ModelContainer.GetModel(ModelID);
        }
    }
}
