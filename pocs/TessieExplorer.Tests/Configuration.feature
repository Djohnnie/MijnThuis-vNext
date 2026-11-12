Feature: Configure the Tessie explorer
  Settings are combined from optional JSON files and environment variables.

  Scenario: Base settings are used when development settings are absent
    Given the "appsettings.json" configuration file contains:
      """
      {"Tessie":{"Token":"base-token","Vin":"base-vin"}}
      """
    When the configuration is loaded
    Then the token is "base-token" and the VIN is "base-vin"

  Scenario: Development settings override base settings per key
    Given the "appsettings.json" configuration file contains:
      """
      {"Tessie":{"Token":"base-token","Vin":"base-vin"}}
      """
    And the "appsettings.Development.json" configuration file contains:
      """
      {"Tessie":{"Token":"development-token"}}
      """
    When the configuration is loaded
    Then the token is "development-token" and the VIN is "base-vin"

  Scenario: Development settings work without a base file
    Given the "appsettings.Development.json" configuration file contains:
      """
      {"Tessie":{"Token":"development-token","Vin":"development-vin"}}
      """
    When the configuration is loaded
    Then the token is "development-token" and the VIN is "development-vin"

  Scenario: Environment variables override JSON settings
    Given the "appsettings.Development.json" configuration file contains:
      """
      {"Tessie":{"Token":"development-token","Vin":"development-vin"}}
      """
    And the environment contains:
      | name          | value          |
      | Tessie__Token | standard-token |
      | Tessie__Vin   | standard-vin   |
    When the configuration is loaded
    Then the token is "standard-token" and the VIN is "standard-vin"

  Scenario: Existing environment variable aliases override other settings
    Given the "appsettings.json" configuration file contains:
      """
      {"Tessie":{"Token":"base-token","Vin":"base-vin"}}
      """
    And the environment contains:
      | name          | value          |
      | Tessie__Token | standard-token |
      | Tessie__Vin   | standard-vin   |
      | TESSIE_TOKEN  | alias-token    |
      | TESSIE_VIN    | alias-vin      |
    When the configuration is loaded
    Then the token is "alias-token" and the VIN is "alias-vin"

  Scenario: JSON alias keys do not override standard environment variables
    Given the "appsettings.json" configuration file contains:
      """
      {"TESSIE_TOKEN":"file-token","TESSIE_VIN":"file-vin"}
      """
    And the environment contains:
      | name          | value          |
      | Tessie__Token | standard-token |
      | Tessie__Vin   | standard-vin   |
    When the configuration is loaded
    Then the token is "standard-token" and the VIN is "standard-vin"

  Scenario: Missing optional settings remain unavailable
    When the configuration is loaded
    Then the token and VIN are unavailable

  Scenario Outline: Invalid JSON is rejected
    Given the "<file>" configuration file contains:
      """
      {invalid json}
      """
    When the configuration is loaded
    Then configuration loading fails

    Examples:
      | file                         |
      | appsettings.json             |
      | appsettings.Development.json |
