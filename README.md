# eShopLite - Semantic Search with Chroma DB

eShopLite is a reference .NET application that implements an eCommerce site with advanced search features, including both keyword and semantic search capabilities. This version utilizes [Chroma DB](https://devblogs.microsoft.com/dotnet/announcing-chroma-db-csharp-sdk/), an open-source database designed for AI applications, to enhance semantic search functionality.

## Features

- **Keyword Search**: Traditional search based on exact keyword matches.
- **Semantic Search**: Leverages Chroma DB to understand the context and intent behind user queries, providing more relevant search results.
- **GitHub Codespaces Integration**: Easily deploy and run the solution entirely in the browser using GitHub Codespaces.

## Architecture Diagram

![Architecture Diagram](images/architecture.png)

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started) (for running Chroma DB locally)
- [ChromaDB.Client NuGet package](https://www.nuget.org/packages/ChromaDB.Client)

### Setting Up Chroma DB

1. **Run Chroma DB Locally**: Use the Chroma Docker image to start a local instance of Chroma DB.

   ```bash
   docker run -p 8000:8000 chromadb/chroma
   ```

2. **Connect to Chroma DB in Your Application**: Install the `ChromaDB.Client` NuGet package and configure the client in your application.

   ```csharp
   using ChromaDB.Client;

   var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
   using var httpClient = new HttpClient();
   var client = new ChromaClient(configOptions, httpClient);
   ```

3. **Create a Collection**: Create a collection in Chroma DB to store your product data.

   ```csharp
   var collection = await client.GetOrCreateCollection("products");
   var collectionClient = new ChromaCollectionClient(collection, configOptions, httpClient);
   ```

4. **Add Data to the Collection**: Add your product data, including embeddings and metadata, to the collection.

   ```csharp
   var embeddings = new List<float[]> { /* your embeddings */ };
   var metadatas = new List<Dictionary<string, object>> { /* your metadata */ };
   var ids = new List<string> { /* your product IDs */ };

   await collectionClient.AddAsync(embeddings, metadatas, ids);
   ```

### Running the Application

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/YourUsername/eShopLite-SemanticSearch-ChromaDB.git
   cd eShopLite-SemanticSearch-ChromaDB/src
   ```

2. **Configure the Application**: Update the configuration files to include your Chroma DB connection settings.

3. **Run the Application**:

   ```bash
   dotnet run --project eShopLite-Aspire.sln
   ```

## Deploying to Azure

To deploy the application to Azure:

1. **Set Up Azure Resources**: Use the Azure Developer CLI to create the necessary resources.

   ```bash
   azd up
   ```

2. **Configure Azure Settings**: Update your application settings to include the Azure-hosted Chroma DB endpoint and any other required configurations.

3. **Deploy the Application**:

   ```bash
   azd deploy
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

---

By integrating Chroma DB into eShopLite, you can provide users with a more intuitive and context-aware search experience, enhancing overall user engagement and satisfaction. 