using ChromaDB.Client;
using DataEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using Newtonsoft.Json;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Products.Models;
using SearchEntities;
using System.Text;

namespace Products.Memory;

public class MemoryContext
{
    private const string SystemPrompt = "You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.";

    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly ChatClient? _chatClient;
    private readonly EmbeddingClient? _embeddingClient;

    private bool _isMemoryCollectionInitialized = false;
    private ChromaCollectionClient? _collectionClient;
    private HttpClient? _httpChromaClient;

    public MemoryContext(ILogger logger, IConfiguration config, ChatClient? chatClient, EmbeddingClient? embeddingClient)
    {
        _logger = logger;
        _config = config;
        _chatClient = chatClient;
        _embeddingClient = embeddingClient;
    }

    public async Task<bool> InitMemoryContextAsync(Context db)
    {
        string? chromaDbUri = GetChromaDbUri();

        var configOptions = new ChromaConfigurationOptions(uri: chromaDbUri);
        _httpChromaClient = new HttpClient();
        var client = new ChromaClient(configOptions, _httpChromaClient);
        var collection = await client.GetOrCreateCollection("products");
        _collectionClient = new ChromaCollectionClient(collection, configOptions, _httpChromaClient);

        _logger.LogInformation("Get a copy of the list of products");
        var products = await db.Product.ToListAsync();

        _logger.LogInformation("Filling products in memory");

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

        _logger.LogInformation("DONE! Filling products in memory");
        return true;
    }

    private string GetChromaDbUri()
    {
        var chromaDbUri = string.Empty;

        _logger.LogInformation("Get ChromaDb from services");
        var chromaDbService = _config.GetSection("services:chroma:chromaendpoint:0");
        chromaDbUri = chromaDbService.Value;
        _logger.LogInformation($"ChromaDB client configuration, key: {chromaDbService.Key}");

        if (!string.IsNullOrEmpty(chromaDbUri) && !chromaDbUri.EndsWith("/api/v1/"))
        {
            _logger.LogInformation("ChromaDB connection string does not end with /api/v1/, adding it");
            chromaDbUri += "/api/v1/";
        }

        _logger.LogInformation($"ChromaDB client uri: {chromaDbUri}");
        return chromaDbUri;
    }

    public async Task<SearchResponse> Search(string search, Context db)
    {
        if (!_isMemoryCollectionInitialized)
        {
            await InitMemoryContextAsync(db);
            _isMemoryCollectionInitialized = true;
        }

        var response = new SearchResponse
        {
            Response = $"I don't know the answer for your question. Your question is: [{search}]"
        };

        try
        {
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
                    var foundProductId = int.Parse(result.Id);
                    var foundProduct = await db.FindAsync<Product>(foundProductId);
                    if (foundProduct != null)
                    {
                        response.Products.Add(foundProduct);
                        sbFoundProducts.AppendLine($"- Product {productPosition}:");
                        sbFoundProducts.AppendLine($"  - Name: {foundProduct.Name}");
                        sbFoundProducts.AppendLine($"  - Description: {foundProduct.Description}");
                        sbFoundProducts.AppendLine($"  - Price: {foundProduct.Price}");
                        productPosition++;
                    }
                }
            }

            // let's improve the response message
            var prompt = @$"You are an intelligent assistant helping clients with their search about outdoor products. 
Generate a catchy and friendly message using the information below.
Add a comparison between the products found and the search criteria.
Include products details.
    - User Question: {search}
    - Found Products: 
{sbFoundProducts}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(prompt)
            };

            _logger.LogInformation("{ChatHistory}", JsonConvert.SerializeObject(messages));

            var resultPrompt = await _chatClient.CompleteChatAsync(messages);
            response.Response = resultPrompt.Value.Content[0].Text!;
        }
        catch (Exception ex)
        {
            // Handle exceptions (log them, rethrow, etc.)
            response.Response = $"An error occurred: {ex.Message}";
        }
        return response;
    }
}