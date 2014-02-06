﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Description;

namespace Swashbuckle.Models
{
    public class SwaggerGenerator
    {
        protected const string SwaggerVersion = "1.2";

        private readonly Func<ApiDescription, string> _declarationKeySelector;
        private readonly Func<string> _basePathResolver;
        private readonly OperationSpecGenerator _operationSpecGenerator;

        public SwaggerGenerator(
            Func<ApiDescription, string> declarationKeySelector,
            Func<string> basePathResolver,
            IDictionary<Type, ModelSpec> customTypeMappings,
            IEnumerable<IOperationFilter> operationFilters,
            IEnumerable<IOperationSpecFilter> operationSpecFilters)
        {
            _declarationKeySelector = declarationKeySelector;
            _basePathResolver = basePathResolver;

            _operationSpecGenerator = new OperationSpecGenerator(customTypeMappings, operationFilters, operationSpecFilters);
        }

        public SwaggerSpec ApiExplorerToSwaggerSpec(IApiExplorer apiExplorer)
        {
            var apiDescriptionGroups = apiExplorer.ApiDescriptions
                .GroupBy(apiDesc => "/" + _declarationKeySelector(apiDesc))
                .OrderBy(group => group.Key)
                .ToArray();

            return new SwaggerSpec
                {
                    Listing = CreateListing(apiDescriptionGroups),
                    Declarations = CreateDeclarations(apiDescriptionGroups)
                };
        }

        private ResourceListing CreateListing(IEnumerable<IGrouping<string, ApiDescription>> apiDescriptionGroups)
        {
            var declarationLinks = apiDescriptionGroups
                .Select(apiDescGrp => new ApiDeclarationLink { Path = apiDescGrp.Key })
                .ToArray();

            return new ResourceListing
            {
                ApiVersion = "1.0",
                SwaggerVersion = SwaggerVersion,
                Apis = declarationLinks
            };
        }

        private Dictionary<string, ApiDeclaration> CreateDeclarations(IEnumerable<IGrouping<string, ApiDescription>> apiDescriptionGroups)
        {
            return apiDescriptionGroups
                .ToDictionary(apiDescGrp => apiDescGrp.Key, CreateDeclaration);
        }

        private ApiDeclaration CreateDeclaration(IGrouping<string, ApiDescription> apiDescriptionGroup)
        {
            var modelSpecRegistrar = new ModelSpecRegistrar();

            // Group further by relative path - each group corresponds to an ApiSpec
            var apiSpecs = apiDescriptionGroup
                .GroupBy(RelativePathSansQueryString)
                .Select(apiDescGrp => CreateApiSpec(apiDescGrp, modelSpecRegistrar))
                .OrderBy(apiSpec => apiSpec.Path)
                .ToList();

            return new ApiDeclaration
            {
                ApiVersion = "1.0",
                SwaggerVersion = SwaggerVersion,
                BasePath = _basePathResolver().TrimEnd('/'),
                ResourcePath = apiDescriptionGroup.Key,
                Apis = apiSpecs,
                Models = modelSpecRegistrar.ToDictionary()
            };
        }

        private ApiSpec CreateApiSpec(IGrouping<string, ApiDescription> apiDescriptionGroup, ModelSpecRegistrar modelSpecRegistrar)
        {
            var operationSpecs = apiDescriptionGroup
                .Select(apiDesc => _operationSpecGenerator.ApiDescriptionToOperationSpec(apiDesc, modelSpecRegistrar))
                .OrderBy(operationSpec => operationSpec.Method)
                .ToList();

            return new ApiSpec
            {
                Path = "/" + apiDescriptionGroup.Key,
                Operations = operationSpecs
            };
        }

        private static string RelativePathSansQueryString(ApiDescription apiDescription)
        {
            return apiDescription.RelativePath.Split('?').First();
        }
    }
}
