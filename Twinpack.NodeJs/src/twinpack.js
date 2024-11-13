const { exec } = require('child_process');

class Twinpack {
    constructor(executablePath = 'dotnet ../TwinpackCli.Net/bin/Debug/net8.0/twinpack.dll') {
        this.executablePath = executablePath;
    }

    /**
     * Run a command with specified arguments and parse JSON output.
     * @param {string} command The command to run.
     * @param {Object} args An object of arguments to pass.
     * @returns {Promise<Object>} Parsed JSON output from the command.
     */
    runCommand(command, args = {}) {
        const argsArray = [];
        argsArray.push(command);

        // Add the --json flag to get JSON output
        argsArray.push('--json-output');

        // Convert args object to command line arguments
        for (const [key, value] of Object.entries(args)) {
            if (Array.isArray(value)) {
                value.forEach(val => argsArray.push(`--${key}`, val));
            } else if (typeof value === 'boolean') {
                if (value) argsArray.push(`--${key}`);
            } else if (value !== null && value !== undefined) {
                argsArray.push(`--${key}`, value);
            }
        }

        const commandStr = `${this.executablePath} ${argsArray.join(' ')}`;
        return new Promise((resolve, reject) => {
            exec(commandStr, (error, stdout, stderr) => {
                if (error) {
                    reject(new Error(`${stdout} ${stderr}`));
                } else {
                    try {
                        const json = JSON.parse(stdout);
                        resolve(json);
                    } catch (parseError) {
                        reject(new Error(`Failed to parse JSON output: ${parseError.message}`));
                    }
                }
            });
        });
    }

    /**
     * Search for packages with optional filters.
     * @param {string} searchTerm Term to filter packages by name or keyword.
     * @param {number} take Limit the number of results.
     * @returns {Promise<Object>} JSON object with search results.
     */
    search({ searchTerm = null, take = null } = {}) {
        return this.runCommand('search', { 'search-term': searchTerm, take });
    }

    /**
     * Configure package servers.
     * @param {Object} options Configuration options.
     * @returns {Promise<Object>} JSON object with the configuration result.
     */
    config({ types = [], sources = [], names = [], usernames = [], passwords = [], purge = false, reset = false } = {}) {
        return this.runCommand('config', {
            type: types,
            source: sources,
            name: names,
            username: usernames,
            password: passwords,
            purge,
            reset
        });
    }
}

module.exports = Twinpack;
