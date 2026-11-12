Feature: Explore Tessie vehicle data
  An explorer can inspect available vehicle data without sending vehicle commands.

  Scenario: Display vehicle state with metric units and missing fields
    Given Tessie returns the following response for "State":
      """
      {"vehicle_state":{"locked":false},"charge_state":{"charging_state":"Charging","usable_battery_level":72,"est_battery_range":100,"charge_miles_added_rated":10,"charge_energy_added":2.5,"charger_actual_current":16,"charge_current_request_max":32,"charge_limit_soc":80,"charge_port_door_open":true},"climate_state":{"inside_temp":21,"outside_temp":-2,"is_preconditioning":true,"cabin_overheat_protection":"On"}}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text        |
      | Charging    |
      | 72 %        |
      | 160.93 km   |
      | 16.09 km    |
      | 21 °C       |
      | -2 °C       |
      | 2.5 kWh     |
      | Unavailable |

  Scenario: Display battery health for the matching vehicle
    Given Tessie returns the following response for "BatteryHealth":
      """
      {"results":[{"vin":"OTHER","health_percent":20},{"vin":"5yjygdee0lf000001","health_percent":91.5}]}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text   |
      | 91.5 % |
    And the console does not display "20 %"

  Scenario: Display a location with an unavailable saved location
    Given Tessie returns the following response for "Location":
      """
      {"address":"[Home]","saved_location":null}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text        |
      | [Home]      |
      | Unavailable |

  Scenario: Display each charge with local timestamps
    Given Tessie returns the following response for "Charges":
      """
      {"results":[{"id":123,"started_at":1700000000,"ended_at":1700003600,"location":"Home","saved_location":"Garage","is_supercharger":false,"is_fast_charger":true,"odometer":2000,"energy_added":30,"energy_used":32,"miles_added":150,"starting_battery":20,"ending_battery":80,"since_last_charge":120},{"id":456}]}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text        |
      | 123         |
      | 456         |
      | Garage      |
      | 150 km      |
      | 32 kWh      |
      | Unavailable |
    And the console displays the local time for 1700000000

  Scenario: Display drive data with metric units
    Given Tessie returns the following response for "Drives":
      """
      {"results":[{"id":987,"started_at":1700000000,"ended_at":1700003600,"starting_location":"A","ending_location":"B","starting_odometer":2000,"ending_odometer":2050,"starting_battery":80,"ending_battery":60,"energy_used":10,"rated_range_used":60,"average_speed":50,"max_speed":100,"odometer_distance":50,"average_inside_temperature":20,"average_outside_temperature":5}]}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text     |
      | 987      |
      | 100 km/h |
      | 20 °C    |
    And the console displays the local time for 1700000000

  Scenario Outline: Identify empty histories
    Given Tessie returns the following response for "<section>":
      """
      {"results":[]}
      """
    When the explorer retrieves the vehicle data
    Then the console displays:
      | text                 |
      | No history available |

    Examples:
      | section |
      | Charges |
      | Drives  |

  Scenario: Report an unsuccessful request without exposing its response
    Given Tessie returns HTTP 401 with the body "private response" for "State"
    When the explorer retrieves the vehicle data
    Then the request fails with "401"
    And the failure does not expose "private response"
    And the failure does not expose "test-token"

  Scenario: Reject a malformed history response
    Given Tessie returns the following response for "Charges":
      """
      {}
      """
    When the explorer retrieves the vehicle data
    Then the request fails with "Response has no results array"
