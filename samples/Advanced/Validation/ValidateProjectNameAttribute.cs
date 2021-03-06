﻿using Spectre.Cli;

namespace Sample.Validation
{
    public sealed class ValidateProjectNameAttribute : ParameterValidationAttribute
    {
        public ValidateProjectNameAttribute() 
            : base(null)
        {
        }

        public override ValidationResult Validate(object value)
        {
            if (!(value is string project))
            {
                return ValidationResult.Error("Package must be a string.");
            }

            if (!project.EndsWith(".csproj"))
            {
                return ValidationResult.Error("Provided project is not a csproj file.");
            }

            return ValidationResult.Success();
        }
    }
}
