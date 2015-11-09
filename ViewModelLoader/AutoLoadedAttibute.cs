using System;


namespace ViewModelLoader
{
    /// <summary>
    /// Mark property that should be loaded with IViewModelLoader and can be loaded by request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AutoLoadedAttibute: Attribute
    {
         
    }
}