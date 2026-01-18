using Xunit;
using FluentAssertions;
using Boxty.SharedBase.Helpers;

namespace Boxty.SharedBase.Tests.Helpers
{
    public class PasswordHelperTests
    {
        [Fact]
        public void GenerateTemporaryPassword_ShouldReturnNonEmptyPassword()
        {
            // Act
            var password = PasswordHelper.GenerateTemporaryPassword();

            // Assert
            password.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GenerateTemporaryPassword_ShouldReturnPasswordOfLength8()
        {
            // Act
            var password = PasswordHelper.GenerateTemporaryPassword();

            // Assert
            password.Should().HaveLength(8);
        }

        [Fact]
        public void GenerateTemporaryPassword_ShouldGenerateDifferentPasswords()
        {
            // Act
            var password1 = PasswordHelper.GenerateTemporaryPassword();
            var password2 = PasswordHelper.GenerateTemporaryPassword();
            var password3 = PasswordHelper.GenerateTemporaryPassword();

            // Assert
            // At least one should be different (very high probability)
            var allSame = password1 == password2 && password2 == password3;
            allSame.Should().BeFalse();
        }

        [Fact]
        public void GenerateTemporaryPassword_ShouldContainOnlyAllowedCharacters()
        {
            // Arrange
            string allowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789$£%^&*()_+!@#";

            // Act
            var password = PasswordHelper.GenerateTemporaryPassword();

            // Assert
            password.All(c => allowedCharacters.Contains(c)).Should().BeTrue();
        }
    }
}
