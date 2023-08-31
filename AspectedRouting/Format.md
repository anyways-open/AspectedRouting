# Building a profile

A profile is a .json-file, which consists of a few metadatafields (which are simple strings),
some expressions to calculate values based on tags, a list of parameters with their default values and
a list of profiles containing the actual parameter value for that profile.


# Vehicle.json

- Metadata: these tags will be copied to the routerdb, but can not be used for routeplanning. A prime example is `name` (the streetname), as it is useful for routeplanning but very useful for navigation afterwards
- vehicletypes: used for turn restrictions, legacy for use with itinero 1.0
- defaults: a dictionary of `{"#paramName": "value"}`, used in determining the weight of an edge. note: the `#` has to be included
- `access` is a field in the vehicle file. It should be an expression returning a string. If (and only if) this string is `no`, the way will be marked as not accessible and no more values will be calculated. All other values are regarded as being accessible. When calculated, the tag `access` with the calculated value is written into the tag table for the other aspects to use.
 - `oneway` is a field in the vehicle file. It should be an expression returning `both`, `with` or `against`. 
 When calculated, the tag `oneway` is added to the tags for the other aspects to be calculated.
 - `speed`: an expression indicating how fast the vehicle can go there. It should take into account legal, practical and social aspects. An example expression could be `{"$min", ["$legal_maxspeed", "#defaultspeed"]}`
- `obstacleaccess` and `obstaclecost` are two (optional) expressions that calculate whether an obstacle can be passed and if so, if there is a penalty for this. See detailed explanations below
 
- `priorities`: a table of `{'#paramName', expression}` determining the priority (1/cost) of a way, per meter. The formula used is `paramName * expression + paramName0 * expression0 + ...` (`speed`, `access` and `oneway` can be used here as tags indicate the earlier defined respective aspects). Use a weight == 1 to get the shortest route or `$speed` to get the fastest route
- `scalingfactor` works in tandem with priority. The _total_ resistance of a link is `priority * scalingfactor`. If this function returns `null`, this will be defaulted to 1.


# Calculating oneway and forward/backward speeds

There are two possibilities in order to calculate the possible direction of a traveller can go over an edge:

1) This can be indicated explicitely with the 'oneway'-field. If this expression returns `both`, the edge is traversable in two directions. If it is either `with` or `against`, then it is not
2) This can be indicated with having a speed or factor which is equal to (or smaller then) 0, in conjunction with a `_direction=with` or `_direction=against` tag. Bicycle lanes are an excellent example for this: a `cycleway:right=yes; cycleway:left=no`, the speed and factor for `_direction=with` could be far greater then for `_direction=against`

Note that `_direction=with` and `_direction=against` are _not_ supported in Itinero1.0 profiles. For maximal compatibility and programming comfort, a mixture of both techniques should be used. For example, one aspect interpreting the legal onewayness in tandem with one aspect determining comfort by direction is optimal.

# Obstacle costs

(Note: this only works with itinero2.0)

Obstacles are objects which are encountered on nodes, e.g. bollards, traffic lights but also turn restrictions.

The first property for this is `obstacleaccess` which calculates wether or not a vehicle can pass the obstacle.
The possible values are:

- "no" of "false": the current vehicle _cannot_ pass this obstacle and should take a different route
- "yes", "true", `null` or any other value: the current vehicle _can_ pass this obstacle. The turn cost will be calculated

If `obstacleaccess` is not `no` or `false`, then `obstaclecost` will be triggered. This possible return values are:

- a positive number, indicating the cost for passing this obstacle
- 0: there is no cost to cross this obstacle
- null: this profile has no knowledge of a cost. Use the default implementation to check for turn restrictions

If the resulting cost is null, the default implementation will be used.

# Functions

- `$funcName` indicates a builtin function
- `#parameter` indicates a configurable parameter

A profile is a function which maps a set of tags onto a value. It is basically Haskell, except that functions can have _multiple_ types. During typechecking, some types will turn out not to be possible and they will dissappear, potentially fixing the implementation of the function itself.

# Injected tags

Aspects can use the following (extra) tags:

- `_direction` is the value to indicate if the traveller goes forward or backwards. It is not available when calculating the 'oneway' field
- `access` will become the value calculated in the field `access` (not available to calculate access)
- `oneway` will become the value calculated in the field `oneway` (not available to calculate access and speed)

## Why is `access`, `oneway` and `speed` copied onto the tags?

`speed` is copied in order for the profile to be able to make a `fast` profile (and accessible as `$speed`).
Access is copied so that `highway=cycleway` and `highway=cycleway;bicycle=designated` will render the same result.

## I still need to access the _original_ value for `access` and `_oneway`

They are copied into `_access` and `_oneway` before any calculations happen.


# Pitfalls

"$and" (which can be interpreted as an _all_ function) should not be used together with a mapping: it checks if all _present_ keys return true or yes (or some other value); it does _not_ check that all the specified keys in the mapping are present.

For this, an additional 'mustMatch' should be added added .

Typical usage of an `if`-statement is:

```
{
 "$ifdotted": {
   "$mustmatch": {
      "key":"somevalue",
      "otherkey": "somevalue"
   }
 },
 "then": <expression>
 "else": <expression>
}

```
