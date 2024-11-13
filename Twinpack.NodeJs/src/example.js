const Twinpack = require('./twinpack');

// Initialize the Twinpack library
const twinpack = new Twinpack();

async function runExamples() {
    try {
        console.log('\nRunning Config Command...');
        const configResults = await twinpack.config({
            types: [],
            sources: [],
            names: [],
            usernames: [],
            passwords: [],
            purge: true,
            reset: true
        });
        console.log('Config Results:', configResults);

        console.log('Running Search Command...');
        const searchResults = await twinpack.search({ searchTerm: 'ZCore', take: 5 });
        console.log('Search Results:', searchResults);
    } catch (error) {
        console.error('Error:', error.message);
    }
}

runExamples();