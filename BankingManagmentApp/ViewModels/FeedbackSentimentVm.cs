namespace BankingManagmentApp.ViewModels
{
    public class FeedbackSentimentVm
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Sentiment { get; set; } = "Neutral";
    }
}
