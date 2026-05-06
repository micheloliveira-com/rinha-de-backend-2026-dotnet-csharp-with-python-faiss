public class VectorService(
    NormalizationConfig normalizationConfig,
    MccRiskConfig mccRiskConfig
)
{
    public double[] BuildVector(FraudRequest req)
    {
        static double Clamp(double v)
            => Math.Max(0.0, Math.Min(1.0, v));

        var tx = req.Transaction;
        var customer = req.Customer;
        var merchant = req.Merchant;
        var terminal = req.Terminal;
        var last = req.LastTransaction;

        var v = new double[14];

        v[0] = Clamp(tx.Amount / normalizationConfig.MaxAmount);
        v[1] = Clamp(tx.Installments / normalizationConfig.MaxInstallments);

        var ratio = customer.AvgAmount == 0 ? 0 : tx.Amount / customer.AvgAmount;
        v[2] = Clamp(ratio / normalizationConfig.AmountVsAvgRatio);

        var dt = ParseIsoUtc(tx.RequestedAt);
        v[3] = dt.Hour / 23.0;

        int dow = ((int)dt.DayOfWeek + 6) % 7;
        v[4] = dow / 6.0;

        if (last == null)
            v[5] = -1;
        else
        {
            var lastTime = ParseIsoUtc(last.Timestamp);
            v[5] = Clamp((dt - lastTime).TotalMinutes / normalizationConfig.MaxMinutes);
        }

        v[6] = last == null ? -1 : Clamp(last.KmFromCurrent / normalizationConfig.MaxKm);
        v[7] = Clamp(terminal.KmFromHome / normalizationConfig.MaxKm);
        v[8] = Clamp(customer.TxCount24h / normalizationConfig.MaxTxCount24h);

        v[9] = terminal.IsOnline ? 1 : 0;
        v[10] = terminal.CardPresent ? 1 : 0;

        v[11] = customer.KnownMerchants.Contains(merchant.Id) ? 0 : 1;

        v[12] = mccRiskConfig.Values.TryGetValue(merchant.Mcc, out var risk)
            ? risk
            : 0.5;

        v[13] = Clamp(merchant.AvgAmount / normalizationConfig.MaxMerchantAvgAmount);

        return v;
    }

    private DateTime ParseIsoUtc(string s)
    {
        int y = (s[0] - '0') * 1000 + (s[1] - '0') * 100 + (s[2] - '0') * 10 + (s[3] - '0');
        int M = (s[5] - '0') * 10 + (s[6] - '0');
        int d = (s[8] - '0') * 10 + (s[9] - '0');
        int h = (s[11] - '0') * 10 + (s[12] - '0');
        int m = (s[14] - '0') * 10 + (s[15] - '0');
        int sec = (s[17] - '0') * 10 + (s[18] - '0');

        return new DateTime(y, M, d, h, m, sec, DateTimeKind.Utc);
    }
}