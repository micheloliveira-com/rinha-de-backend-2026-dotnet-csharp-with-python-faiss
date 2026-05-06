public class FraudService(
    FaissClient faissClient,
    VectorService vectorService
)
{
    public async Task<FraudResponse> Process(FraudRequest fraudRequest)
    {
        
        var vector = vectorService.BuildVector(fraudRequest)
            .Select(x => (float)x)
            .ToArray();

        var result = await faissClient.QueryAsync(vector);

        double score = result.FraudCount / 5.0;

        return new FraudResponse(
            Approved: score < 0.6,
            FraudScore: score
        );
    }
}