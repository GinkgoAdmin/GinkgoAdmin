# My Plugin

A basic plugin template for the GinkgoAdmin plugin system.

## Features

- Basic plugin structure
- Hook system integration
- Component registration
- Dependency management

## Installation

1. Copy this template to `web/src/plugins/installed/my-plugin/`
2. Modify the plugin configuration in `plugin.json`
3. Implement your plugin logic in `index.ts`
4. Add any required dependencies to the configuration
5. Restart the application to load the plugin

## Configuration

Edit `plugin.json` to configure your plugin:

```json
{
  "name": "my-plugin",
  "version": "1.0.0",
  "description": "Your plugin description",
  "author": "Your Name",
  "cdnDependencies": [
    {
      "name": "library-name",
      "type": "cdn",
      "url": "https://cdn.example.com/library.js",
      "required": true
    }
  ],
  "npmDependencies": [
    {
      "name": "package-name",
      "type": "npm",
      "version": "^1.0.0",
      "required": true
    }
  ]
}
```

## Development

### Adding Dependencies

#### CDN Dependencies
```typescript
cdnDependencies: [
  {
    name: 'jquery',
    type: 'cdn',
    url: 'https://code.jquery.com/jquery-3.6.0.min.js',
    required: true
  }
]
```

#### NPM Dependencies
```typescript
npmDependencies: [
  {
    name: 'lodash',
    type: 'npm',
    version: '^4.17.21',
    required: true,
    installCommand: 'npm install lodash@^4.17.21'
  }
]
```

### Registering Components

```typescript
api.registerComponent('my-component', MyComponent)
```

### Adding Hooks

```typescript
api.addHook('slot:my-slot', (data) => {
  return {
    component: MyComponent,
    props: { ...data }
  }
}, 10)
```

## API Reference

### Plugin API

- `api.addHook(hookName, handler, priority)` - Register a hook
- `api.removeHook(hookName, handler)` - Remove a hook
- `api.registerComponent(name, component)` - Register a Vue component
- `api.loadDependency(dependency)` - Load a dependency
- `api.checkDependency(name)` - Check if dependency is loaded
- `api.loadAsset(asset)` - Load a static asset
- `api.executeCommand(command)` - Execute a shell command
- `api.getConfig()` - Get plugin configuration
- `api.log(message, level)` - Log a message

### Hook System

Available hooks:
- `slot:*` - Slot injection hooks
- `editor:*` - Editor-related hooks
- `auth:*` - Authentication hooks
- `plugin:*` - Plugin lifecycle hooks

## License

MIT