# eShopLite - Semantic Search with Chroma DB

eShopLite is a reference .NET application that implements an eCommerce site with advanced search features, including both keyword and semantic search capabilities. This version utilizes [Chroma DB](https://devblogs.microsoft.com/dotnet/announcing-chroma-db-csharp-sdk/), an open-source database designed for AI applications, to enhance semantic search functionality.

## Features

- **Keyword Search**: Traditional search based on exact keyword matches.
- **Semantic Search**: Leverages Chroma DB to understand the context and intent behind user queries, providing more relevant search results.
- **GitHub Codespaces Integration**: Easily deploy and run the solution entirely in the browser using GitHub Codespaces.

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet)
- [Docker](https://www.docker.com/get-started) (for running SQL and Chroma DB locally)

### Chroma DB Overview

1. The AspireHost project will be in charge to **Run Chroma DB Locally**: 

   Instead of using the the Chroma Docker image to start a local instance of Chroma DB.

   ```bash
   docker run -p 8000:8000 chromadb/chroma
   ```

   Aspire Host will create a persistent container for Chroma DB, which will be used to store the embeddings and metadata for the products.

   ```csharp
    var chromaDB = builder.AddContainer("chroma", "chromadb/chroma")
        .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "chromaendpoint")
        .WithLifetime(ContainerLifetime.Persistent);

    var endpoint = chromaDB.GetEndpoint("chromaendpoint");

    var products = builder.AddProject<Projects.Products>("products")
        .WithReference(endpoint)
        .WithReference(sqldb)
        .WaitFor(sqldb);
   ```

1. **Connect to Chroma DB in Your Application**: 

   The `ChromaDB.Client` NuGet package will allow the connections to the ChromaDB in your `Products` project.

   ```csharp
   using ChromaDB.Client;

   var chromaDbService = _config.GetSection("services:chroma:chromaendpoint:0");
   var chromaDbUri = chromaDbService.Value;

   var configOptions = new ChromaConfigurationOptions(uri: $"{chromaDbUri}/api/v1/");
   _httpChromaClient = new HttpClient();
   var client = new ChromaClient(configOptions, _httpChromaClient);
   ```

1. **Create a Collection**: Create a collection in Chroma DB to store your product data.

   ```csharp
   var collection = await client.GetOrCreateCollection("products");
   _collectionClient = new ChromaCollectionClient(collection, configOptions, _httpChromaClient);
   ```

1. **Add Data to the Collection**: Add your product data, including embeddings and metadata, to the collection.

   ```csharp
    var productIds = new List<string>();
    var productDescriptionEmbeddings = new List<ReadOnlyMemory<float>>();
    var productMetadata = new List<Dictionary<string, object>>();

    // iterate over the products and add them to the memory
    foreach (var product in products)
    {
        try
        {
            _logger.LogInformation("Adding product to memory: {Product}", product.Name);
            var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";
            var result = await _embeddingClient.GenerateEmbeddingAsync(productInfo);
            productIds.Add(product.Id.ToString());
            productDescriptionEmbeddings.Add(result.Value.ToFloats());
            _logger.LogInformation($"Product added to collections: {product.Name}");
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "Error adding product to memory");
        }
    }

    // add the products to the memory
    await _collectionClient.Upsert(productIds, productDescriptionEmbeddings, productMetadata);

   ```

1. **Search products**: search the products using the Chroma DB client.

   ```csharp
    var resultGenEmbeddings = await _embeddingClient.GenerateEmbeddingAsync(search);
    var embeddingsSearchQuery = resultGenEmbeddings.Value.ToFloats();

    var searchOptions = new VectorSearchOptions
    {
        Top = 1,
        VectorPropertyName = "Vector"
    };

    // search the vector database for the most similar product        
    var queryResult = await _collectionClient.Query(
        queryEmbeddings: embeddingsSearchQuery,
        nResults: 2,
        include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);

    var sbFoundProducts = new StringBuilder();
    int productPosition = 1;
    foreach (var result in queryResult)
    {
        if (result.Distance > 0.3)
        {
            // product found, magic happens here
        }
    }
   ```

### Running the Application

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/elbruno/eShopLite-ChromaDB.git
   cd eShopLite-ChromaDB/src
   ```

2. **Configure the Application**: Update the configuration files to include your Chroma DB connection settings.

3. **Run the Application**:

   ```bash
   dotnet run --project eShopLite-ChromaDB.sln
   ```

## Resources

- [Chroma DB C# SDK Announcement](https://devblogs.microsoft.com/dotnet/announcing-chroma-db-csharp-sdk/)
- [eShopLite Semantic Search Sample](https://github.com/Azure-Samples/eShopLite-SemanticSearch)

## Video Demonstrations

- COMING SOON! [Run eShopLite Semantic Search with Chroma DB in Minutes](https://youtu.be/YourVideoLink)

## Guidance

### Costs

Be aware of potential costs associated with running Chroma DB instances and other Azure resources. Monitor your usage to manage expenses effectively.

### Security Guidelines

- **Authentication**: Ensure secure authentication mechanisms are in place when connecting to Chroma DB and other services.
- **Data Privacy**: Handle user data responsibly and comply with relevant data protection regulations.

For more detailed security practices, refer to the [Microsoft Security Documentation](https://docs.microsoft.com/security/).
