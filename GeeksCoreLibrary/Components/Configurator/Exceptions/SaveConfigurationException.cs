using System;
using GeeksCoreLibrary.Components.Configurator.Enums;

namespace GeeksCoreLibrary.Components.Configurator.Exceptions;

public class SaveConfigurationException : Exception
{
    public SaveConfigurationErrorCodes ErrorCode { get; set; }

    public SaveConfigurationException()
    {
    }

    public SaveConfigurationException(string message) : base(message)
    {
    }

    public SaveConfigurationException(string message, Exception inner) : base(message, inner)
    {
    }
}