public class WarmupService(FaissClient faissClient)
{
    public async Task Warmup()
    {
        Console.WriteLine("[FAISS] Starting warmup batch...");

        var rand = new Random();
        var vector = new float[14]; // reused buffer

        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < vector.Length; j++)
            {
                vector[j] = (float)rand.NextDouble();
            }

            try
            {
                await faissClient.QueryAsync(vector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAISS] Warmup failed at iter {i}: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("[FAISS] Warmup batch completed successfully.");
    }
}