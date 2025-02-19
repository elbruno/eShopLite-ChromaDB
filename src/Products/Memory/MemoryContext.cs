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

public class MemoryContext(ILogger logger, IConfiguration config, ChatClient? chatClient, EmbeddingClient? embeddingClient)
{
    private string _systemPrompt = "";
    private bool _isMemoryCollectionInitialized = false;
    ChromaCollectionClient collectionClient = null;
    HttpClient httpClient = null;

    public async Task<bool> InitMemoryContextAsync(Context db)
    {
        var chromaDbService = config.GetSection("services:chroma:chromaendpoint:0");
        var chromaDbUri = chromaDbService.Value;

        var configOptions = new ChromaConfigurationOptions(uri: $"{chromaDbUri}/api/v1/");
        httpClient = new HttpClient();
        var client = new ChromaClient(configOptions, httpClient);
        var collection = await client.GetOrCreateCollection("products");
        collectionClient = new ChromaCollectionClient(collection, configOptions, httpClient);


        // define system prompt
        _systemPrompt = "You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.";

        logger.LogInformation("Get a copy of the list of products");
        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        logger.LogInformation("Filling products in memory");

        List<string> productIds = [];
        List<ReadOnlyMemory<float>> productDescriptionEmbeddings = [];
        List<Dictionary<string, object>> productMetadata = [];

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            try
            {
                logger.LogInformation("Adding product to memory: {Product}", product.Name);
                var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";
                var result = await embeddingClient.GenerateEmbeddingAsync(productInfo);
                productIds.Add(product.Id.ToString());
                productDescriptionEmbeddings.Add(result.Value.ToFloats());
                productMetadata.Add(new Dictionary<string, object>
                {
                    { "Name", product.Name },
                    { "Description", product.Description },
                    { "Price", product.Price },
                    { "ImageUrl", product.ImageUrl }
                });

                logger.LogInformation($"Product added to collections: {product.Name}");
            }
            catch (Exception exc)
            {
                logger.LogError(exc, "Error adding product to memory");
            }
        }

        // add the products to the memory
        collectionClient.Add(productIds, productDescriptionEmbeddings, productMetadata);


        logger.LogInformation("DONE! Filling products in memory");
        return true;
    }

    public async Task<SearchResponse> Search(string search, Context db)
    {
        if (!_isMemoryCollectionInitialized)
        {
            await InitMemoryContextAsync(db);
            _isMemoryCollectionInitialized = true;
        }

        var response = new SearchResponse();
        response.Response = $"I don't know the answer for your question. Your question is: [{search}]";
        var responseText = "";
        try
        {
            var resultGenEmbeddings = await embeddingClient.GenerateEmbeddingAsync(search);
            var embeddingsSearchQuery = resultGenEmbeddings.Value.ToFloats();

            var searchOptions = new VectorSearchOptions()
            {
                Top = 1,
                VectorPropertyName = "Vector"
            };

            // search the vector database for the most similar product        
            var queryResult = await collectionClient.Query(
                queryEmbeddings: embeddingsSearchQuery,
                nResults: 2,
                include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);

            double searchScore = 0.0;
            foreach (var result in queryResult)
            {
                if (result.Distance > 0.3)
                {
                    int foundProductId = int.Parse(result.Id);
                    var foundProduct = db.Find<Product>(foundProductId);
                    responseText = $"The product [{foundProduct.Name}] fits with the search criteria [{search}][{result.Distance.ToString("0.00")}]";
                    logger.LogInformation($"Search Response: {responseText}");
                    response.Products.Add(foundProduct);
                }
            }

            StringBuilder sbFoundProducts = new();
            int productPosition = 1;
            foreach (var foundProduct in response.Products)
            {
                if (foundProduct != null)
                {
                    sbFoundProducts.AppendLine($"- Product {productPosition}:");
                    sbFoundProducts.AppendLine($"  - Name: {foundProduct.Name}");
                    sbFoundProducts.AppendLine($"  - Description: {foundProduct.Description}");
                    sbFoundProducts.AppendLine($"  - Price: {foundProduct.Price}");
                    productPosition++;
                }
            }


            // let's improve the response message
            var prompt = @$"You are an intelligent assistant helping clients with their search about outdoor products. 
Generate a catchy and friendly message using the information below.
Add a comparison between the products found and the search criteria.
Include products details.
    - User Question: {search}
    - Found Products: 
{sbFoundProducts.ToString()}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(_systemPrompt),
                new UserChatMessage(prompt)
            };

            logger.LogInformation("{ChatHistory}", JsonConvert.SerializeObject(messages));

            var resultPrompt = await chatClient.CompleteChatAsync(messages);
            responseText = resultPrompt.Value.Content[0].Text!;

            response.Response = responseText;

        }
        catch (Exception ex)
        {
            // Handle exceptions (log them, rethrow, etc.)
            response.Response = $"An error occurred: {ex.Message}";
        }
        return response;
    }
}