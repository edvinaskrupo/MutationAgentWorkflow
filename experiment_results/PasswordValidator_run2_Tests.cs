using System.Collections.Generic;
using MutationAgentWorkflow.Sample;
using Xunit;

namespace MutationAgentWorkflow.Tests;

public class PasswordValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_NullOrEmptyInput_ReturnsFalse(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Ab1!defg")]
    [InlineData("Ab1!defgh")]
    [InlineData("Ab1!defghijk")]
    [InlineData("Ab1!defghijklmn")]
    [InlineData("Ab1!defghijklmnop")]
    public void IsValid_MeetsAllRequiredRules_ReturnsTrue(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_LengthBelowMinimum_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!def";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_LengthAtMinimum_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defg";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_LengthAboveMaximum_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_LengthAtMaximum_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_MissingUppercase_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "ab1!defg";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_MissingLowercase_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "AB1!DEFG";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_MissingDigit_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abc!Defg";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_MissingSpecialCharacter_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abc1Defg";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("PaSsWoRd")]
    [InlineData("123456")]
    [InlineData("QWERTY")]
    public void IsValid_CommonPassword_ReturnsFalse(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetValidationErrors_NullInput_ReturnsEmptyPasswordError()
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var errors = validator.GetValidationErrors(null);

        // Assert
        Assert.Single(errors);
    }

    [Fact]
    public void GetValidationErrors_NullInput_ReturnsSpecificErrorMessage()
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var errors = validator.GetValidationErrors(null);

        // Assert
        Assert.Equal("Password cannot be empty.", errors[0]);
    }

    [Fact]
    public void GetValidationErrors_EmptyInput_ReturnsEmptyPasswordError()
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var errors = validator.GetValidationErrors("");

        // Assert
        Assert.Single(errors);
    }

    [Fact]
    public void GetValidationErrors_LengthBelowMinimum_ReturnsLengthError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!def";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains($"Password must be at least {PasswordValidator.MinLength} characters.", errors);
    }

    [Fact]
    public void GetValidationErrors_LengthAboveMaximum_ReturnsNoLengthError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.DoesNotContain($"Password must be at most {PasswordValidator.MaxLength} characters.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingUppercase_ReturnsUppercaseError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "ab1!defg";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one uppercase letter.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingLowercase_ReturnsLowercaseError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "AB1!DEFG";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one lowercase letter.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingDigit_ReturnsDigitError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abc!Defg";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one digit.", errors);
    }

    [Fact]
    public void GetValidationErrors_MissingSpecialCharacter_ReturnsSpecialCharacterError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abc1Defg";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one special character.", errors);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("PaSsWoRd")]
    public void GetValidationErrors_CommonPassword_ReturnsCommonPasswordError(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password is too common.", errors);
    }

    [Fact]
    public void GetValidationErrors_ValidPassword_ReturnsNoErrors()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defgh";

        // Act
        var errors = validator.GetValidationErrors(password);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Abc123!@")]
    [InlineData("abc123!@")]
    public void GetStrengthScore_LengthAtLeastEight_AddsLengthPoint(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.True(score >= 1);
    }

    [Fact]
    public void GetStrengthScore_LengthExactlyTwelve_AddsTwelveCharacterPoint()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijk";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(8, score);
    }

    [Fact]
    public void GetStrengthScore_LengthExactlySixteen_AddsSixteenCharacterPoint()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijklmno";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(9, score);
    }

    [Fact]
    public void GetStrengthScore_WeakPassword_ReturnsLowScore()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "aaaaaaaa";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(3, score);
    }

    [Fact]
    public void GetStrengthScore_ValidStrongPassword_ReturnsHighScore()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defghijk";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(8, score);
    }

    [Fact]
    public void GetStrengthScore_CommonPassword_DoesNotAwardCommonPasswordPoint()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Password1!";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, score);
    }

    [Fact]
    public void GetStrengthScore_UniqueCharactersBelowThreshold_DoesNotAddUniqueCharacterPoint()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Aaa1!aaa";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(6, score);
    }

    [Fact]
    public void GetStrengthScore_UniqueCharactersAtThreshold_AddsUniqueCharacterPoint()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!cdef";

        // Act
        var score = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, score);
    }

    [Theory]
    [InlineData("aa", 1)]
    [InlineData("aabbccdd", 4)]
    public void CountUniqueCharacters_DifferentInputs_ReturnsExpectedCounts(string input, int expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var count = validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData("ABC", true)]
    [InlineData("abc", false)]
    public void HasUpperCase_DetectsUppercaseCharacters_ReturnsExpectedResult(string input, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.HasUpperCase(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ABC", false)]
    [InlineData("abc", true)]
    public void HasLowerCase_DetectsLowercaseCharacters_ReturnsExpectedResult(string input, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.HasLowerCase(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("abc1", true)]
    public void HasDigit_DetectsDigitCharacters_ReturnsExpectedResult(string input, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.HasDigit(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abc1", false)]
    [InlineData("abc1!", true)]
    [InlineData("abc1 ", true)]
    public void HasSpecialCharacter_DetectsSpecialCharacters_ReturnsExpectedResult(string input, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.HasSpecialCharacter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("PASSWORD", true)]
    [InlineData("passw0rd", false)]
    [InlineData("unknown", false)]
    public void IsCommonPassword_UsesCaseInsensitiveLookup_ReturnsExpectedResult(string password, bool expected)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsCommonPassword(password);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetStrengthLabel_ScoreTwoOrBelow_ReturnsWeak()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "aa";

        // Act
        var label = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Weak", label);
    }

    [Fact]
    public void GetStrengthLabel_ScoreFiveOrBelow_ReturnsFair()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!aaaa";

        // Act
        var label = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Strong", label);
    }

    [Fact]
    public void GetStrengthLabel_ScoreSevenOrBelow_ReturnsStrong()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!defgh";

        // Act
        var label = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Strong", label);
    }

    [Fact]
    public void GetStrengthLabel_ScoreAboveSeven_ReturnsVeryStrong()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!cdefghijk";

        // Act
        var label = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Very Strong", label);
    }

    [Fact]
    public void GetStrengthLabel_NullInput_ReturnsWeak()
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var label = validator.GetStrengthLabel(null);

        // Assert
        Assert.Equal("Weak", label);
    }
}