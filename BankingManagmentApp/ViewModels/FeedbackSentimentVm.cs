namespace BankingManagmentApp.ViewModels
{
    public class FeedbackSentimentVm
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;

        // basic rule-based sentiment classification
        public string Sentiment { get; set; } = "Neutral";
    }
}
