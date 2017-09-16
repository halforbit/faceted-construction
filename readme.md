# Facets

The Facets library allows you to describe and make instances of **Faceted Contexts**. These are interfaces decorated with facet attributes. 

```cs
    public interface IDataContext : IContext
    {
        [DataStore, RootPath(TestRootPath), JsonSerialization, GZipCompression]
        IDataStore<string> DataStore { get; }
    }
```

These contexts are **descriptive** and **implementationless** at design time. 

You can use a `ContextFactory` to create an instance of a context at runtime.

```cs
    var context = new ContextFactory().Create<IDataContext>();
```

Facets can be used with [Data Stores](https://github.com/halforbit/data-stores) to create DRY, descriptive, implementationless data contexts.