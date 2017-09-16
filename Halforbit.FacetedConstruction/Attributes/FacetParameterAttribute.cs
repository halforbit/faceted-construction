
namespace Halforbit.FacetedConstruction.Attributes
{
    public abstract class FacetParameterAttribute : FacetAttribute
    {
        public FacetParameterAttribute(
            string value = null, 
            string configKey = null)
        {
            Value = value;

            ConfigKey = configKey;
        }

        public abstract string ParameterName { get; }

        public virtual string Value { get; }

        public virtual string ConfigKey { get; }
    }
}
