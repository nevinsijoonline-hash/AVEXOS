using AVEXOS.Security;
using Xunit;

namespace AVEXOS.Security.Tests;

public class RegistryPolicyTests
{
    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options")]
    public void Denies_Machine_Wide_Keys(string keyPath)
    {
        Assert.False(RegistryPolicy.IsWritable(keyPath, out var reason), reason);
        Assert.Equal("Machine-wide registry writes are not permitted.", reason);
    }

    [Fact]
    public void Allows_Wallpaper_Root_The_App_Actually_Writes()
    {
        Assert.True(RegistryPolicy.IsWritable(@"HKEY_CURRENT_USER\Control Panel\Desktop", out var reason),
            reason);
    }

    [Theory]
    [InlineData(@"HKEY_CURRENT_USER\Environment")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Winlogon")]
    public void Denies_User_Keys_Outside_The_Allowlist(string keyPath)
    {
        Assert.False(RegistryPolicy.IsWritable(keyPath, out var reason), reason);
        Assert.Equal("Registry key is outside the allowed user-scope list.", reason);
    }

    // Spec rule 22: the association hive stays refused even though it is user-scope.
    [Theory]
    [InlineData(@"HKEY_CURRENT_USER\Software\Classes\.avex")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Classes\avex.File\shell\open\command")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell")]
    public void Denies_The_File_Association_Hive(string keyPath)
    {
        Assert.False(RegistryPolicy.IsWritable(keyPath, out var reason),
            $"Spec rule 22: {keyPath} was approved ({reason})");
    }
}
