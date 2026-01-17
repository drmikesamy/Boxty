namespace Boxty.SharedBase.Helpers
{
    public static class PasswordHelper
    {
        public static string GenerateTemporaryPassword()
        {
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789$£%^&*()_+!@#";
            Random random = new Random();
            char[] password = new char[8];
            for (int i = 0; i < password.Length; i++)
            {
                password[i] = characters[random.Next(characters.Length)];
            }
            return new string(password);
        }
    }
}