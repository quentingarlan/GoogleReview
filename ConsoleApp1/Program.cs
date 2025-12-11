using Google.Apis.Auth.OAuth2;
using Google.Apis.MyBusinessAccountManagement.v1;
using Google.Apis.Services;

class Program
{
    // Le scope d'autorisation requis pour Google My Business
    private static readonly string[] Scopes = { "https://www.googleapis.com/auth/business.manage" };
    private const string ApplicationName = "My Business API Validator";

    static async Task Main(string[] args)
    {
        Console.WriteLine("Démarrage du processus de validation d'invitation.");

        try
        {
            // Étape 1 : Authentification de l'utilisateur
            Console.WriteLine("Authentification en cours. Une fenêtre de navigateur va s'ouvrir.");
            UserCredential credential = await GetUserCredentialAsync();

            var accountId = "accounts/";
            var locations = await GetLocationsAsync(credential, accountId);

            foreach (var loc in locations)
            {
                Console.WriteLine($"→ Location : {loc.LocationName}");

                var reviews = await GetReviewsAsync(credential, loc.Name);

                Console.WriteLine($"    {reviews.Count} avis trouvés");

                foreach (var review in reviews)
                {
                    Console.WriteLine($"    ⭐ {review.StarRating} – {review.Comment}");
                }
            }

            // Étape 2 : Créer le service API
            var service = new MyBusinessAccountManagementService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Étape 3 : Lister les comptes pour trouver l'ID
            Console.WriteLine("Récupération des comptes...");
            var accounts = await service.Accounts.List().ExecuteAsync();

            string accountId2 = "";
            if (accounts.Accounts != null && accounts.Accounts.Count > 0)
            {
                // Dans la plupart des cas, il n'y a qu'un seul compte à l'ID
                accountId2 = accounts.Accounts[0].Name;
                Console.WriteLine($"Compte trouvé : {accounts.Accounts[0].AccountName} (ID: {accountId2})");

                foreach (var acc in accounts.Accounts)
                {
                    Console.WriteLine($"{acc.Name} | {acc.Type} | {acc.AccountName}");
                }
            }
            else
            {
                Console.WriteLine("Aucun compte trouvé. Assurez-vous que l'utilisateur a accès à un compte Business Profile.");
                return;
            }

            // Étape 4 : Lister les invitations pour le compte
            Console.WriteLine("Récupération des invitations en attente...");
            var invitations = await service.Accounts.Invitations.List(accountId2).ExecuteAsync();

            if (invitations.Invitations != null && invitations.Invitations.Count > 0)
            {
                var invitation = invitations.Invitations[0];
                Console.WriteLine($"Invitation en attente trouvée pour la localisation : {invitation.TargetLocation?.LocationName}");
                Console.WriteLine($"ID de l'invitation : {invitation.Name}");

                // Étape 5 : Accepter l'invitation
                Console.WriteLine("Acceptation de l'invitation...");
                await service.Accounts.Invitations.Accept(new Google.Apis.MyBusinessAccountManagement.v1.Data.AcceptInvitationRequest(), invitation.Name).ExecuteAsync();

                Console.WriteLine("Invitation acceptée avec succès !");
            }
            else
            {
                Console.WriteLine("Aucune invitation en attente trouvée.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Une erreur est survenue : {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Détails de l'erreur : {ex.InnerException.Message}");
            }
        }
        finally
        {
            Console.WriteLine("Processus terminé. Appuyez sur une touche pour quitter.");
            Console.ReadKey();
        }
    }

    private static async Task<UserCredential> GetUserCredentialAsync()
    {
        // Votre ID client et votre client secret de la console Google Cloud
        var clientId = "";
        var clientSecret = "";
        // Charge les identifiants depuis le fichier client_secrets.json

        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user", // L'ID de l'utilisateur pour le stockage local du jeton
                CancellationToken.None
            );

    }

    private static async Task<List<Review>> GetReviewsAsync(UserCredential credential, string locationName)
    {
        var token = await credential.GetAccessTokenForRequestAsync();
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"https://mybusiness.googleapis.com/v4/{locationName}/reviews";

        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        // Désérialisation (à créer)
        var data = System.Text.Json.JsonSerializer.Deserialize<ReviewsResponse>(json);

        return data?.Reviews ?? new List<Review>();
    }

    public class ReviewsResponse
    {
        public List<Review> Reviews { get; set; }
    }

    public class Review
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public int StarRating { get; set; }
        public ReviewReviewer Reviewer { get; set; }
        public string CreateTime { get; set; }
    }

    public class ReviewReviewer
    {
        public string DisplayName { get; set; }
        public string ProfilePhotoUrl { get; set; }
    }

    private static async Task<List<Location>> GetLocationsAsync(UserCredential credential, string accountId)
    {
        var token = await credential.GetAccessTokenForRequestAsync();
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        //var url = $"https://mybusiness.googleapis.com/v4/{accountId}/locations";
        var url2 = $"https://businessprofile.googleapis.com/v1/accounts/{accountId}/locations";
        var response = await httpClient.GetAsync(url2);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        return System.Text.Json.JsonSerializer.Deserialize<LocationList>(json)?.Locations;
    }

    public class LocationList
    {
        public List<Location> Locations { get; set; }
    }

    public class Location
    {
        public string Name { get; set; }
        public string LocationName { get; set; }
    }
}