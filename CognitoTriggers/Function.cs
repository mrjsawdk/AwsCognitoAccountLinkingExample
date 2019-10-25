using System;
using System.Net;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using CognitoTriggers.Exceptions;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CognitoTriggers
{
    public class Function
    {
        private readonly AmazonCognitoIdentityProviderClient _client;

        public Function()
        {
            _client = new AmazonCognitoIdentityProviderClient();
        }

        public dynamic Execute(dynamic input, ILambdaContext context)
        {
            string triggerSource = input.triggerSource;

            try
            {
                if (triggerSource.Equals("PreSignUp_ExternalProvider"))
                {
                    string email = input.request?.userAttributes?["email"];

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        throw new PreSignupTriggerException("email not found on incoming identity with username:" + input.userName);
                    }

                    Console.Write("Pre-signup triggered with : " + JsonConvert.SerializeObject(input));

                    //Find an existing user with the same email
                    UserType existingUser = FindDestinationUser(input, email);

                    Console.WriteLine($"Found existing user with email:{email}. User is: {JsonConvert.SerializeObject(existingUser)}");

                    (string provider, string id) identity = ParseIncomingIdentity(input);

                    //Perform account linking
                    var linkResponse = LinkProviderToExistingUser(input, identity.provider, identity.id, existingUser);

                    if (linkResponse.HttpStatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Failed to link users:{linkResponse.HttpStatusCode}");
                    }
                    else
                    {
                        Console.WriteLine("Linked users");

                        //NOTE: Make sure Cognito does not create a new user for this one by short-circuiting the creation process
                        return new { version = 1 }; //TODO: How to prevent failure but redirect to authentication
                    }
                }
                else
                {
                    //Allow the trigger to continue
                    return input;
                }
            }
            catch (PreSignupTriggerException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new PreSignupTriggerException("Unknown error occurred");
            }

            return null;
        }

        private static (string provider, string id) ParseIncomingIdentity(dynamic input)
        {
            string compositeId = input.userName;
            var splitIndex = compositeId.IndexOf('_');
            var provider = compositeId.Substring(0, splitIndex);
            var id = compositeId.Substring(splitIndex + 1);

            return (provider, id);
        }

        private UserType FindDestinationUser(dynamic input, string email)
        {
            //Find existing user
            var response = _client.ListUsersAsync(new ListUsersRequest
            {
                Filter = $"email=\"{email}\"",
                Limit = 1,
                UserPoolId = input.userPoolId
            }).Result;

            //Handle bad status code
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Failed to search for users:{response.HttpStatusCode}");
                throw new PreSignupTriggerException("Unable to search for existing account to pair with");
            }

            //No user found. Since auto sign-up is not allowed on this pool, throw an exception
            if (response.Users.Count == 0)
            {
                Console.WriteLine($"No existing users found with email:{email}");
                throw new PreSignupTriggerException("No existing user with same email found");
            }

            //If user found, link the new identity to the existing user
            return response.Users[0];
        }

        private AdminLinkProviderForUserResponse LinkProviderToExistingUser(
            dynamic input,
            string provider,
            string id,
            UserType existingUser)
        {
            //Link the incoming identity to the existing user. Will add an "identity" to the "Identities" array, and allow the existing user to login using the new social login
            var linkResponse = _client.AdminLinkProviderForUserAsync(
                new AdminLinkProviderForUserRequest
                {
                    UserPoolId = input.userPoolId,
                    SourceUser = new ProviderUserIdentifierType
                    {
                        ProviderName = provider,
                        ProviderAttributeName = "Cognito_Subject",
                        ProviderAttributeValue = id
                    },
                    DestinationUser = new ProviderUserIdentifierType
                    {
                        ProviderName = "Cognito",
                        ProviderAttributeValue = existingUser.Username
                    }
                }).Result;
            return linkResponse;
        }
    }
}
