using System;
using System.Collections.Generic;
using MutationAgentWorkflow.Sample;
using Xunit;

public class PasswordValidatorTests
{
    private readonly PasswordValidator _validator = new PasswordValidator();

    [Fact]
    public void IsValid_NullPassword_ReturnsFalse()
    {
        // Arrange
        string password = null;

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_EmptyPassword_ReturnsFalse()
    {
        // Arrange
        string password = string.Empty;

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("A1!bcdef", true)]
    [InlineData("A1!bcde", false)]
    public void IsValid_BoundaryLengthPasswords_ReturnsExpectedResult(string password, bool expected)
    {
        // Arrange

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("A1!bcdef", true)]
    [InlineData("A1!bcdefg", true)]
    [InlineData("A1!bcdefghijklm", true)]
    [InlineData("abcdefg1!", false)]
    [InlineData("ABCDEFG1!", false)]
    [InlineData("Abcdefgh!", false)]
    [InlineData("Abcdefgh1", false)]
    [InlineData("password1!", false)]
    public void IsValid_VariousPasswordShapes_ReturnsExpectedResult(string password, bool expected)
    {
        // Arrange

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValid_CommonPasswordDifferentCasing_ReturnsFalse()
    {
        // Arrange
        string password = "Password";

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_ValidPasswordWithAllRequiredTypes_ReturnsTrue()
    {
        // Arrange
        string password = "Abcdef1!";

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_PasswordWithWhitespaceAndAllRequiredTypes_ReturnsTrue()
    {
        // Arrange
        string password = "Abc def1!";

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_VeryLongValidPasswordNearMaxLength_ReturnsTrue()
    {
        // Arrange
        string password = "Abcdef1!Abcdef1!Abcdef1!Abcdef1!Abcdef1!Abcdef1!Abcdef1!Abcd";

        // Act
        bool result = _validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetValidationErrors_NullPassword_ReturnsSingleEmptyError()
    {
        // Arrange
        string password = null;

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Single(errors);
    }

    [Fact]
    public void GetValidationErrors_NullPassword_ReturnsEmptyErrorMessage()
    {
        // Arrange
        string password = null;

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Equal("Password cannot be empty.", errors[0]);
    }

    [Fact]
    public void GetValidationErrors_EmptyPassword_ReturnsSingleEmptyError()
    {
        // Arrange
        string password = string.Empty;

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Single(errors);
    }

    [Fact]
    public void GetValidationErrors_TooShortPassword_IncludesMinimumLengthError()
    {
        // Arrange
        string password = "A1!bcde";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains($"Password must be at least {PasswordValidator.MinLength} characters.", errors);
    }

    [Fact]
    public void GetValidationErrors_TooLongPassword_IncludesMaximumLengthError()
    {
        // Arrange
        string password = new string('A', 65) + "1!a";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains($"Password must be at most {PasswordValidator.MaxLength} characters.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingUppercase_IncludesUppercaseError()
    {
        // Arrange
        string password = "abcde1!f";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one uppercase letter.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingLowercase_IncludesLowercaseError()
    {
        // Arrange
        string password = "ABCDE1!F";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one lowercase letter.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingDigit_IncludesDigitError()
    {
        // Arrange
        string password = "Abcdefg!";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one digit.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingSpecialCharacter_IncludesSpecialCharacterError()
    {
        // Arrange
        string password = "Abcdefg1";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one special character.", errors);
    }

    [Fact]
    public void GetValidationErrors_CommonPassword_IncludesCommonPasswordError()
    {
        // Arrange
        string password = "password";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password is too common.", errors);
    }

    [Fact]
    public void GetValidationErrors_ValidPassword_ReturnsNoErrors()
    {
        // Arrange
        string password = "Abcdef1!";

        // Act
        List<string> errors = _validator.GetValidationErrors(password);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("A1!bcdef", 7, "Strong")]
    [InlineData("A1!bcdefgh", 7, "Strong")]
    [InlineData("A1!bcdefghij", 8, "Very Strong")]
    [InlineData("A1!bcdefghijkl", 8, "Very Strong")]
    [InlineData("A1!bcdefghijklmn", 9, "Very Strong")]
    [InlineData("A1!bcdefghijklmnop", 9, "Very Strong")]
    public void GetStrengthLabel_ScoreThresholds_ReturnsExpectedLabel(string password, int expectedScore, string expectedLabel)
    {
        // Arrange

        // Act
        int score = _validator.GetStrengthScore(password);
        string label = _validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal(expectedScore, score);
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void GetStrengthLabel_NullPassword_ReturnsWeak()
    {
        // Arrange
        string password = null;

        // Act
        string label = _validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Weak", label);
    }

    [Fact]
    public void GetStrengthLabel_EmptyPassword_ReturnsWeak()
    {
        // Arrange
        string password = string.Empty;

        // Act
        string label = _validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Weak", label);
    }

    [Fact]
    public void GetStrengthScore_NullPassword_ReturnsZero()
    {
        // Arrange
        string password = null;

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(0, score);
    }

    [Fact]
    public void GetStrengthScore_CommonPassword_ReturnsLowScore()
    {
        // Arrange
        string password = "password";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(2, score);
    }

    [Fact]
    public void GetStrengthScore_ExactMinLengthWithAllRequiredTypes_ReturnsExpectedScore()
    {
        // Arrange
        string password = "Abcdef1!";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, score);
    }

    [Fact]
    public void GetStrengthScore_JustBelowTwelveCharacters_ReturnsExpectedScore()
    {
        // Arrange
        string password = "Abcdef1!";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, score);
    }

    [Fact]
    public void GetStrengthScore_ExactlyTwelveCharacters_AddsSecondLengthPoint()
    {
        // Arrange
        string password = "Abcdef1!ghij";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(8, score);
    }

    [Fact]
    public void GetStrengthScore_ExactlySixteenCharacters_AddsThirdLengthPoint()
    {
        // Arrange
        string password = "Abcdef1!ghijklmn";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(9, score);
    }

    [Fact]
    public void GetStrengthScore_ValidPasswordWithUniqueCharactersBelowEight_ReturnsExpectedScore()
    {
        // Arrange
        string password = "Aa1!Aa1!";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(6, score);
    }

    [Fact]
    public void GetStrengthScore_ValidPasswordWithExactlyEightUniqueCharacters_AddsUniqueCharacterPoint()
    {
        // Arrange
        string password = "Abcdef1!";

        // Act
        int score = _validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, score);
    }

    [Fact]
    public void HasUpperCase_InputWithUppercase_ReturnsTrue()
    {
        // Arrange
        string input = "abcD";

        // Act
        bool result = _validator.HasUpperCase(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasUpperCase_InputWithoutUppercase_ReturnsFalse()
    {
        // Arrange
        string input = "abcd";

        // Act
        bool result = _validator.HasUpperCase(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasLowerCase_InputWithLowercase_ReturnsTrue()
    {
        // Arrange
        string input = "ABCd";

        // Act
        bool result = _validator.HasLowerCase(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasLowerCase_InputWithoutLowercase_ReturnsFalse()
    {
        // Arrange
        string input = "ABCD";

        // Act
        bool result = _validator.HasLowerCase(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasDigit_InputWithDigit_ReturnsTrue()
    {
        // Arrange
        string input = "abc1";

        // Act
        bool result = _validator.HasDigit(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasDigit_InputWithoutDigit_ReturnsFalse()
    {
        // Arrange
        string input = "abcd";

        // Act
        bool result = _validator.HasDigit(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasSpecialCharacter_InputWithSpecialCharacter_ReturnsTrue()
    {
        // Arrange
        string input = "abc!";

        // Act
        bool result = _validator.HasSpecialCharacter(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasSpecialCharacter_InputWithoutSpecialCharacter_ReturnsFalse()
    {
        // Arrange
        string input = "abc1";

        // Act
        bool result = _validator.HasSpecialCharacter(input);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("Password", true)]
    [InlineData("PASSWORD", true)]
    [InlineData("notcommon", false)]
    public void IsCommonPassword_KnownValues_ReturnsExpectedResult(string password, bool expected)
    {
        // Arrange

        // Act
        bool result = _validator.IsCommonPassword(password);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CountUniqueCharacters_RepeatedCharacters_ReturnsOne()
    {
        // Arrange
        string input = "aaaaaa";

        // Act
        int count = _validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountUniqueCharacters_AllUniqueCharacters_ReturnsInputLength()
    {
        // Arrange
        string input = "abcdef";

        // Act
        int count = _validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(input.Length, count);
    }

    [Fact]
    public void CountUniqueCharacters_InputWithWhitespaceAndUnicode_ReturnsExpectedCount()
    {
        // Arrange
        string input = "a aé!";

        // Act
        int count = _validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(4, count);
    }
}